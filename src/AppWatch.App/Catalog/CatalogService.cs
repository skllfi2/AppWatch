using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using AppWatch.Core.Domain;
using AppWatch.Core.Domain.Models;

namespace AppWatch.Catalog;

/// <summary>
/// Seed catalog shipped inside the app + user catalog in %APPDATA%\AppWatch\user_catalog.json.
/// User entries override seed entries by owner/repo; user-only entries are appended.
/// The catalog is a starting point, NOT a closed list — users add their own.
/// </summary>
public sealed class CatalogService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        WriteIndented = true,
    };

    private readonly string _userCatalogPath;

    private List<CatalogEntry> _seedEntries = [];
    private List<CatalogEntry> _userEntries = [];

    /// <summary>Merged view: seed + user, user overrides seed by owner/repo.</summary>
    public IReadOnlyList<CatalogEntry> Entries { get; private set; } = [];

    public IReadOnlyList<CatalogEntry> UserEntries => _userEntries;

    public CatalogService()
    {
        _userCatalogPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AppWatch", "user_catalog.json");
        Load();
    }

    public bool IsUserEntry(string repoOwner, string repoName) =>
        CatalogMerger.IsUserEntry(_userEntries, repoOwner, repoName);

    /// <summary>Add or update a user entry. Returns true if saved.</summary>
    public bool AddUserEntry(CatalogEntry entry)
    {
        var existing = _userEntries.FindIndex(e =>
            string.Equals(e.RepoOwner, entry.RepoOwner, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(e.RepoName, entry.RepoName, StringComparison.OrdinalIgnoreCase));
        if (existing >= 0)
            _userEntries[existing] = entry;
        else
            _userEntries.Add(entry);

        return PersistUserCatalog();
    }

    /// <summary>Remove a user entry. Returns true if found and removed.</summary>
    public bool RemoveUserEntry(string repoOwner, string repoName)
    {
        var removed = _userEntries.RemoveAll(e =>
            string.Equals(e.RepoOwner, repoOwner, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(e.RepoName, repoName, StringComparison.OrdinalIgnoreCase));
        if (removed == 0) return false;
        return PersistUserCatalog();
    }

    private void Load()
    {
        _seedEntries = LoadSeed();
        _userEntries = LoadUserFile();
        Entries = CatalogMerger.Merge(_seedEntries, _userEntries);
    }

    public void Reload()
    {
        Load();
    }

    private static List<CatalogEntry> LoadSeed()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream("AppWatch.Catalog.catalog.json");
        if (stream is null) return [];
        return JsonSerializer.Deserialize<CatalogFile>(stream, Options)?.Entries ?? [];
    }

    private List<CatalogEntry> LoadUserFile()
    {
        if (!File.Exists(_userCatalogPath)) return [];
        try
        {
            var json = File.ReadAllText(_userCatalogPath);
            return JsonSerializer.Deserialize<CatalogFile>(json, Options)?.Entries ?? [];
        }
        catch
        {
            return []; // corrupted user file — don't crash, fall back to seed only
        }
    }

    private bool PersistUserCatalog()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_userCatalogPath)!);
            var file = new CatalogFile { Entries = _userEntries };
            File.WriteAllText(_userCatalogPath, JsonSerializer.Serialize(file, Options));
            Entries = CatalogMerger.Merge(_seedEntries, _userEntries);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private sealed class CatalogFile
    {
        public List<CatalogEntry> Entries { get; set; } = [];
    }
}
