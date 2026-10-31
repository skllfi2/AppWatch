using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using AppWatch.Core.Abstractions;
using AppWatch.Core.Domain.Models;

namespace AppWatch.Infrastructure.Config;

public sealed class JsonConfigStore : IConfigStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly string _path;
    private readonly object _sync = new();

    public JsonConfigStore(string? directory = null)
    {
        var dir = directory
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AppWatch");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "config.json");
    }

    public AppConfig Load()
    {
        lock (_sync)
        {
            if (!File.Exists(_path)) return new AppConfig();
            try
            {
                var config = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(_path), Options) ?? new AppConfig();
                var migrated = Migrate(config);
                if (migrated)
                    Save(config);
                return config;
            }
            catch (Exception ex) when (ex is JsonException or IOException)
            {
                try
                {
                    var backup = _path + ".corrupted." + DateTime.UtcNow.ToString("yyyyMMddHHmmss");
                    File.Copy(_path, backup, overwrite: true);
                    Serilog.Log.Error(ex, "Failed to deserialize {Path}. Preserved corrupt copy at {Backup}", _path, backup);
                }
                catch { }
                return new AppConfig();
            }
        }
    }

    public void Save(AppConfig config)
    {
        lock (_sync)
        {
            var tmp = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                var json = JsonSerializer.Serialize(config, Options);
                File.WriteAllText(tmp, json);
                File.Move(tmp, _path, overwrite: true);
            }
            catch (Exception ex) when (ex is IOException)
            {
                Thread.Sleep(50);
                try { File.Move(tmp, _path, overwrite: true); } catch { }
            }
            finally
            {
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
            }
        }
    }

    private static bool Migrate(AppConfig config)
    {
        if (config.SchemaVersion < 2)
        {
            config.HealthCheckSeconds = 6;
            config.SchemaVersion = 2;
            return true;
        }
        return false;
    }
}
