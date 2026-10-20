using System.Text.Json;
using AppWatch.Core.Domain;

namespace AppWatch.Infrastructure;

/// <summary>
/// UPD-02: file-backed journal for in-flight updates (one operation at a time —
/// the gate serializes mutations, so a single record is enough). Lives in
/// %APPDATA%\AppWatch next to the config; corrupt/missing file = no pending op.
/// </summary>
public sealed class UpdateJournalStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly string _path;

    public UpdateJournalStore()
    {
        _path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AppWatch", PendingUpdate.FileName);
    }

    /// <summary>Path of the journal file (exposed for tests/diagnostics).</summary>
    public string JournalPath => _path;

    public PendingUpdate? Load()
    {
        try
        {
            if (!File.Exists(_path)) return null;
            return JsonSerializer.Deserialize<PendingUpdate>(File.ReadAllText(_path), Options);
        }
        catch
        {
            return null; // corrupt journal is not a reason to refuse starting the app
        }
    }

    public void Write(PendingUpdate pending)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(pending, Options));
    }

    public void Clear()
    {
        try { File.Delete(_path); }
        catch (IOException) { /* best effort */ }
        catch (UnauthorizedAccessException) { /* best effort */ }
    }
}
