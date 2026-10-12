using System.Text.Json;
using AppWatch.Core.Domain.Models;

namespace AppWatch.Infrastructure;

/// <summary>Persists update results to %APPDATA%\AppWatch\history.json.</summary>
public sealed class UpdateHistoryStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly string _path;

    public UpdateHistoryStore(string? directory = null)
    {
        var dir = directory
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AppWatch");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "history.json");
    }

    public IReadOnlyList<UpdateHistoryEntry> Load()
    {
        if (!File.Exists(_path)) return [];
        return JsonSerializer.Deserialize<List<UpdateHistoryEntry>>(File.ReadAllText(_path), Options) ?? [];
    }

    public void Append(UpdateHistoryEntry entry)
    {
        var history = Load().ToList();
        history.Add(entry);
        File.WriteAllText(_path, JsonSerializer.Serialize(history, Options));
    }
}
