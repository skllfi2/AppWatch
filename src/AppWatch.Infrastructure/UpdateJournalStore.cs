using System.Text.Json;
using AppWatch.Core.Domain;

namespace AppWatch.Infrastructure;

/// <summary>
/// UPD-02: file-backed journal for in-flight updates (one operation at a time —
/// the gate serializes mutations, so a single record is enough). Lives in
/// %APPDATA%\AppWatch next to the config; corrupt/missing file = no pending op.
/// Writes atomically via temporary files to avoid corruption during crashes.
/// </summary>
public sealed class UpdateJournalStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly string _path;

    public UpdateJournalStore(string? path = null)
    {
        _path = path ?? Path.Combine(
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
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        var tmp = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(tmp, JsonSerializer.Serialize(pending, Options));
            File.Move(tmp, _path, overwrite: true);
        }
        finally
        {
            try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
        }
    }

    public void Clear()
    {
        try { File.Delete(_path); }
        catch (IOException) { /* best effort */ }
        catch (UnauthorizedAccessException) { /* best effort */ }
    }
}
