using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using AppWatch.Core.Domain.Models;

namespace AppWatch.Infrastructure;

/// <summary>Persists update results to %APPDATA%\AppWatch\history.json with thread-safe atomic writes and bounded retention.</summary>
public sealed class UpdateHistoryStore
{
    public const int MaxEntries = 500;

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly string _path;
    private readonly object _sync = new();

    public UpdateHistoryStore(string? directory = null)
    {
        var dir = directory
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AppWatch");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "history.json");
    }

    public IReadOnlyList<UpdateHistoryEntry> Load()
    {
        lock (_sync)
        {
            if (!File.Exists(_path)) return [];
            try
            {
                return JsonSerializer.Deserialize<List<UpdateHistoryEntry>>(File.ReadAllText(_path), Options) ?? [];
            }
            catch
            {
                return [];
            }
        }
    }

    public void Append(UpdateHistoryEntry entry)
    {
        lock (_sync)
        {
            var history = Load().ToList();
            history.Add(entry);

            // Bounded retention: keep the most recent entries to avoid unbounded file growth
            if (history.Count > MaxEntries)
                history = history.Skip(history.Count - MaxEntries).ToList();

            var tmp = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(tmp, JsonSerializer.Serialize(history, Options));
                File.Move(tmp, _path, overwrite: true);
            }
            finally
            {
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
            }
        }
    }

    /// <summary>Clears the entire update history log.</summary>
    public void Clear()
    {
        lock (_sync)
        {
            try
            {
                if (File.Exists(_path))
                    File.Delete(_path);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
