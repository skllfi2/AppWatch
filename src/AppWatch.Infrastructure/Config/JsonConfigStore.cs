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
            if (!File.Exists(_path))
            {
                return new AppConfig { Language = GetDefaultSystemLanguage() };
            }

            try
            {
                var config = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(_path), Options) ?? new AppConfig();
                var deduplicated = DeduplicateTrackedApps(config);
                var migrated = Migrate(config);
                if (deduplicated || migrated)
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
                return new AppConfig { Language = GetDefaultSystemLanguage() };
            }
        }
    }

    private static string GetDefaultSystemLanguage()
    {
        var sys = global::System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant();
        return sys switch
        {
            "ru" => "ru",
            "uk" => "ru",
            "be" => "ru",
            "kk" => "ru",
            "uz" => "ru",
            "tg" => "ru",
            "ky" => "ru",
            _ => "en"
        };
    }

    public void Save(AppConfig config)
    {
        lock (_sync)
        {
            var json = JsonSerializer.Serialize(config, Options);
            var tmp = _path + ".tmp." + Guid.NewGuid().ToString("N");
            try
            {
                File.WriteAllText(tmp, json);
                File.Move(tmp, _path, overwrite: true);
            }
            finally
            {
                if (File.Exists(tmp))
                {
                    try { File.Delete(tmp); } catch { }
                }
            }
        }
    }

    private static bool DeduplicateTrackedApps(AppConfig config)
    {
        if (config.TrackedApps is null || config.TrackedApps.Count <= 1) return false;

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var deduped = new List<TrackedApp>();
        var changed = false;

        foreach (var app in config.TrackedApps)
        {
            var key = !string.IsNullOrWhiteSpace(app.RepoOwner) && !string.IsNullOrWhiteSpace(app.RepoName)
                ? $"gh:{app.RepoOwner.Trim()}/{app.RepoName.Trim()}"
                : (!string.IsNullOrWhiteSpace(app.WingetPackageId)
                    ? $"winget:{app.WingetPackageId.Trim()}"
                    : $"name:{app.Name.Trim()}");

            if (seen.Add(key))
            {
                deduped.Add(app);
            }
            else
            {
                changed = true;
            }
        }

        if (changed)
        {
            config.TrackedApps = deduped;
        }

        return changed;
    }

    private static bool Migrate(AppConfig config)
    {
        var changed = false;

        // Migrations go here as schema evolves
        // e.g. if (config.SchemaVersion < 2) { ... config.SchemaVersion = 2; changed = true; }

        return changed;
    }
}
