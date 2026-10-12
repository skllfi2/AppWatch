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
        Migrate(config);
        return config;
    }

    public void Save(AppConfig config) =>
        File.WriteAllText(_path, JsonSerializer.Serialize(config, Options));

    private static void Migrate(AppConfig config)
    {
        // migrations run oldest-first; each bumps SchemaVersion
        // if (config.SchemaVersion < 2) { ...; config.SchemaVersion = 2; }
    }
}
