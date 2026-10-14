using System.Text.Json;
using System.Text.Json.Serialization;
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

    public JsonConfigStore(string? directory = null)
    {
        var dir = directory
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AppWatch");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "config.json");
    }

    public AppConfig Load()
    {
        if (!File.Exists(_path)) return new AppConfig();
        var config = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(_path), Options) ?? new AppConfig();
        var migrated = Migrate(config);
        if (migrated)
            Save(config);
        return config;
    }

    public void Save(AppConfig config)
    {
        // D20: atomic write — temp file + rename (avoids torn writes on crash)
        var tmp = _path + ".tmp";
        var json = JsonSerializer.Serialize(config, Options);
        File.WriteAllText(tmp, json);
        File.Move(tmp, _path, overwrite: true);
    }

    private static bool Migrate(AppConfig config)
    {
        // migrations run oldest-first; each bumps SchemaVersion
        if (config.SchemaVersion < 2)
        {
            // Schema v2: added HealthCheckSeconds, BackgroundCheckNextRunUtc, CatalogCheckNextRunUtc
            config.HealthCheckSeconds = 6; // UpdateHealthCheck.DefaultWindowSeconds
            config.SchemaVersion = 2;
            return true;
        }
        return false;
    }
}
