namespace AppWatch.Core.Domain.Models;

public enum CatalogSourceType
{
    /// <summary>Built-in seed catalog — always present, can be hidden but not removed.</summary>
    Seed,
    /// <summary>User's local catalog file (user_catalog.json in AppData).</summary>
    UserFile,
    /// <summary>Remote catalog JSON (GitHub raw, gist, any static URL).</summary>
    RemoteUrl,
}

/// <summary>
/// One catalog source. The user can subscribe to multiple catalogs —
/// each is a JSON file in the same schema as catalog.json.
/// </summary>
public sealed class CatalogSource
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public required string Name { get; set; }
    public CatalogSourceType Type { get; set; }
    /// <summary>HTTP(S) URL for RemoteUrl sources; null for Seed/UserFile.</summary>
    public string? Url { get; set; }
    public bool Enabled { get; set; } = true;
    /// <summary>Last successful remote fetch (UTC) — null if never fetched or not remote.</summary>
    public DateTimeOffset? LastFetchedUtc { get; set; }
    /// <summary>Entry count at last load — for display.</summary>
    public int EntryCount { get; set; }
}
