using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using AppWatch.Core.Domain;
using AppWatch.Core.Domain.Models;

namespace AppWatch.Catalog;

/// <summary>
/// Multi-source catalog: seed (embedded) + user file + remote URLs.
/// Users subscribe to catalogs — each is a JSON file in the same schema as catalog.json.
/// The catalog is a platform, NOT a closed list.
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

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };

    private readonly string _appDataDir;
    private readonly string _userCatalogPath;
    private readonly string _sourcesPath;
    private readonly string _cacheDir;

    private List<CatalogEntry> _seedEntries = [];
    private List<CatalogEntry> _userEntries = [];
    private List<CatalogSource> _sources = [];

    /// <summary>Merged view of all enabled sources.</summary>
    public IReadOnlyList<CatalogEntry> Entries { get; private set; } = [];

    /// <summary>Which source each entry came from: "owner/repo" → source name.</summary>
    public IReadOnlyDictionary<string, string> SourceByRepo { get; private set; } = new Dictionary<string, string>();

    /// <summary>All catalog sources (seed + user + remotes).</summary>
    public IReadOnlyList<CatalogSource> Sources => _sources;

    public IReadOnlyList<CatalogEntry> UserEntries => _userEntries;

    public CatalogService()
    {
        _appDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AppWatch");
        _userCatalogPath = Path.Combine(_appDataDir, "user_catalog.json");
        _sourcesPath = Path.Combine(_appDataDir, "catalog_sources.json");
        _cacheDir = Path.Combine(_appDataDir, "catalog_cache");
        Load();
    }

    public bool IsUserEntry(string repoOwner, string repoName) =>
        CatalogMerger.IsUserEntry(_userEntries, repoOwner, repoName);

    // === User entry management (unchanged) ===

    public bool AddUserEntry(CatalogEntry entry)
    {
        var existing = _userEntries.FindIndex(e =>
            string.Equals(e.RepoOwner, entry.RepoOwner, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(e.RepoName, entry.RepoName, StringComparison.OrdinalIgnoreCase));
        if (existing >= 0)
            _userEntries[existing] = entry;
        else
            _userEntries.Add(entry);
        var ok = PersistUserCatalog();
        if (ok) RebuildMerged();
        return ok;
    }

    public bool RemoveUserEntry(string repoOwner, string repoName)
    {
        var removed = _userEntries.RemoveAll(e =>
            string.Equals(e.RepoOwner, repoOwner, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(e.RepoName, repoName, StringComparison.OrdinalIgnoreCase));
        if (removed == 0) return false;
        var ok = PersistUserCatalog();
        if (ok) RebuildMerged();
        return ok;
    }

    /// <summary>Removes ALL user-added entries ("мои приложения"). Seed/remote sources are untouched.</summary>
    /// <returns>How many entries were removed.</returns>
    public int ClearUserEntries()
    {
        var removed = _userEntries.Count;
        if (removed == 0) return 0;
        _userEntries.Clear();
        PersistUserCatalog();
        RebuildMerged();
        return removed;
    }

    // === Source management ===

    /// <summary>Add a remote catalog source. Downloads immediately; returns null on success, error message on failure.</summary>
    public async Task<string?> AddRemoteSourceAsync(string name, string url)
    {
        var source = new CatalogSource
        {
            Name = name,
            Type = CatalogSourceType.RemoteUrl,
            Url = url,
        };
        var error = await DownloadAndCacheAsync(source);
        if (error is not null) return error;
        _sources.Add(source);
        PersistSources();
        RebuildMerged();
        return null;
    }

    /// <summary>Remove a source (Seed cannot be removed). Returns false if not found or is seed.</summary>
    public bool RemoveSource(string sourceId)
    {
        var source = _sources.FirstOrDefault(s => s.Id == sourceId);
        if (source is null || source.Type == CatalogSourceType.Seed) return false;
        _sources.Remove(source);
        // remove cache
        var cachePath = Path.Combine(_cacheDir, $"{source.Id}.json");
        if (File.Exists(cachePath)) File.Delete(cachePath);
        PersistSources();
        RebuildMerged();
        return true;
    }

    /// <summary>Toggle a source's enabled state. Returns the new state, or null if not found.</summary>
    public bool? ToggleSource(string sourceId)
    {
        var source = _sources.FirstOrDefault(s => s.Id == sourceId);
        if (source is null) return null;
        source.Enabled = !source.Enabled;
        PersistSources();
        RebuildMerged();
        return source.Enabled;
    }

    /// <summary>Refresh a remote source (re-download). Returns null on success, error on failure.</summary>
    public async Task<string?> RefreshSourceAsync(string sourceId)
    {
        var source = _sources.FirstOrDefault(s => s.Id == sourceId);
        if (source is null || source.Type != CatalogSourceType.RemoteUrl) return "not a remote source";
        var error = await DownloadAndCacheAsync(source);
        if (error is not null) return error;
        RebuildMerged();
        return null;
    }

    // === Internal ===

    private void Load()
    {
        _seedEntries = LoadSeed();
        _userEntries = LoadUserFile();
        _sources = LoadSources();
        EnsureSeedSource();
        // load cached remote entries (no network on startup)
        RebuildMerged();
    }

    public void Reload()
    {
        Load();
    }

    private void EnsureSeedSource()
    {
        // Seed is always present and cannot be removed
        if (!_sources.Any(s => s.Type == CatalogSourceType.Seed))
            _sources.Insert(0, new CatalogSource
            {
                Id = "seed",
                Name = "Базовый каталог",
                Type = CatalogSourceType.Seed,
                Enabled = true,
                EntryCount = _seedEntries.Count,
            });
        if (!_sources.Any(s => s.Type == CatalogSourceType.UserFile))
            _sources.Add(new CatalogSource
            {
                Id = "user",
                Name = "Мои приложения",
                Type = CatalogSourceType.UserFile,
                Enabled = true,
                EntryCount = _userEntries.Count,
            });
    }

    private void RebuildMerged()
    {
        var sourceLists = new List<(string, IReadOnlyList<CatalogEntry>)>();
        foreach (var source in _sources.Where(s => s.Enabled))
        {
            var entries = source.Type switch
            {
                CatalogSourceType.Seed => _seedEntries,
                CatalogSourceType.UserFile => _userEntries,
                CatalogSourceType.RemoteUrl => LoadCachedRemote(source),
                _ => [],
            };
            source.EntryCount = entries.Count;
            sourceLists.Add((source.Name, entries));
        }
        var (merged, sourceByRepo) = CatalogMerger.MergeSources(sourceLists);
        Entries = merged;
        SourceByRepo = sourceByRepo;
    }

    private List<CatalogEntry> LoadCachedRemote(CatalogSource source)
    {
        var cachePath = Path.Combine(_cacheDir, $"{source.Id}.json");
        if (!File.Exists(cachePath)) return [];
        try
        {
            return JsonSerializer.Deserialize<CatalogFile>(File.ReadAllText(cachePath), Options)?.Entries ?? [];
        }
        catch { return []; }
    }

    private async Task<string?> DownloadAndCacheAsync(CatalogSource source)
    {
        try
        {
            var response = await Http.GetAsync(source.Url);
            if (!response.IsSuccessStatusCode)
                return $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}";
            var json = await response.Content.ReadAsStringAsync();
            var file = JsonSerializer.Deserialize<CatalogFile>(json, Options);
            if (file?.Entries is null || file.Entries.Count == 0)
                return "файл не содержит записей (entries)";
            Directory.CreateDirectory(_cacheDir);
            File.WriteAllText(Path.Combine(_cacheDir, $"{source.Id}.json"), json);
            source.LastFetchedUtc = DateTimeOffset.UtcNow;
            source.EntryCount = file.Entries.Count;
            return null;
        }
        catch (HttpRequestException ex)
        {
            return $"не удалось скачать: {ex.Message}";
        }
        catch (JsonException ex)
        {
            return $"не удалось разобрать JSON: {ex.Message}";
        }
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
        catch { return []; }
    }

    private List<CatalogSource> LoadSources()
    {
        if (!File.Exists(_sourcesPath)) return [];
        try
        {
            var sourceOptions = new JsonSerializerOptions(Options);
            return JsonSerializer.Deserialize<SourcesFile>(File.ReadAllText(_sourcesPath), sourceOptions)?.Sources ?? [];
        }
        catch { return []; }
    }

    private void PersistSources()
    {
        try
        {
            Directory.CreateDirectory(_appDataDir);
            var file = new SourcesFile { Sources = _sources };
            File.WriteAllText(_sourcesPath, JsonSerializer.Serialize(file, Options));
        }
        catch { /* non-fatal */ }
    }

    private bool PersistUserCatalog()
    {
        try
        {
            Directory.CreateDirectory(_appDataDir);
            var file = new CatalogFile { Entries = _userEntries };
            File.WriteAllText(_userCatalogPath, JsonSerializer.Serialize(file, Options));
            return true;
        }
        catch { return false; }
    }

    private sealed class CatalogFile
    {
        public List<CatalogEntry> Entries { get; set; } = [];
    }

    private sealed class SourcesFile
    {
        public List<CatalogSource> Sources { get; set; } = [];
    }
}
