using System.IO;
using System.Text.Json;
using AppWatch.Core.Domain.Models;
using AppWatch.Infrastructure;

namespace AppWatch.Catalog;

/// <summary>
/// Embedding index over catalog entries: "owner/repo" → vector, cached on disk so a
/// big catalog (stars-import hundreds) is embedded once per changed text, not per
/// app start. Cache file stores {key, textHash, base64 vector}; a changed description
/// re-embeds only that entry.
/// </summary>
public sealed class SemanticCatalogIndex
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };

    private readonly IEmbeddingService _embedding;
    private readonly string _cachePath;
    private readonly Dictionary<string, (int Hash, float[] Vector)> _vectors = new();
    private bool _dirty;

    public SemanticCatalogIndex(IEmbeddingService embedding, string? appDataDir = null)
    {
        _embedding = embedding;
        _cachePath = Path.Combine(
            appDataDir ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AppWatch"),
            "semantic_index.json");
        LoadCache();
    }

    public int IndexedCount => _vectors.Count;

    /// <summary>
    /// Embeds missing/stale entries and persists the cache.
    /// Returns the number of entries successfully indexed.
    /// </summary>
    public async Task<int> BuildAsync(IReadOnlyList<CatalogEntry> entries)
    {
        if (!_embedding.IsReady) return 0;

        var embedded = 0;
        foreach (var e in entries)
        {
            var key = $"{e.RepoOwner}/{e.RepoName}";
            var text = TextOf(e);
            var hash = StringHash(text);

            if (_vectors.TryGetValue(key, out var cached) && cached.Hash == hash)
                continue;

            var vector = await _embedding.EmbedAsync(text);
            if (vector is null) continue;

            _vectors[key] = (hash, vector);
            _dirty = true;
            embedded++;
        }

        // drop vectors for entries that disappeared
        var live = entries.Select(e => $"{e.RepoOwner}/{e.RepoName}").ToHashSet(StringComparer.Ordinal);
        foreach (var stale in _vectors.Keys.Where(k => !live.Contains(k)).ToList())
        {
            _vectors.Remove(stale);
            _dirty = true;
        }

        if (_dirty) SaveCache();
        return _vectors.Count;
    }

    public float[]? VectorOf(CatalogEntry entry) =>
        _vectors.TryGetValue($"{entry.RepoOwner}/{entry.RepoName}", out var v) ? v.Vector : null;

    public void Clear()
    {
        _vectors.Clear();
        _dirty = true;
        SaveCache();
    }

    private static string TextOf(CatalogEntry e) =>
        (e.Description is { Length: > 0 } d) ? $"{e.Name}. {d}" : e.Name;

    private static int StringHash(string s)
    {
        unchecked
        {
            var h = 17;
            foreach (var c in s) h = h * 31 + c;
            return h;
        }
    }

    private void LoadCache()
    {
        try
        {
            if (!File.Exists(_cachePath)) return;
            var entries = JsonSerializer.Deserialize<List<CachedVector>>(File.ReadAllText(_cachePath), JsonOpts);
            if (entries is null) return;
            foreach (var c in entries)
            {
                if (c.Key is null || c.Vector is null) continue;
                _vectors[c.Key] = (c.Hash, Decode(c.Vector));
            }
        }
        catch { _vectors.Clear(); }
    }

    private void SaveCache()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_cachePath)!);
            var list = _vectors
                .Select(kv => new CachedVector { Key = kv.Key, Hash = kv.Value.Hash, Vector = Encode(kv.Value.Vector) })
                .ToList();
            File.WriteAllText(_cachePath, JsonSerializer.Serialize(list, JsonOpts));
            _dirty = false;
        }
        catch { /* cache write is best-effort */ }
    }

    private sealed class CachedVector
    {
        public string? Key { get; set; }
        public int Hash { get; set; }
        public string? Vector { get; set; }
    }

    private static string Encode(float[] v)
    {
        var bytes = new byte[v.Length * sizeof(float)];
        Buffer.BlockCopy(v, 0, bytes, 0, bytes.Length);
        return Convert.ToBase64String(bytes);
    }

    private static float[] Decode(string base64)
    {
        var bytes = Convert.FromBase64String(base64);
        var v = new float[bytes.Length / sizeof(float)];
        Buffer.BlockCopy(bytes, 0, v, 0, bytes.Length);
        return v;
    }
}
