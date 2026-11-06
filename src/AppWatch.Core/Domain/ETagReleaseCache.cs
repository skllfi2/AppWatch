using System.Collections.Concurrent;
using AppWatch.Core.Domain.Models;

namespace AppWatch.Core.Domain;

public sealed record CachedReleaseEntry(
    string ETag,
    IReadOnlyList<ReleaseInfo> Releases,
    DateTimeOffset CachedAtUtc);

/// <summary>
/// Thread-safe in-memory and persistent ETag cache for release queries.
/// Prevents exhausting GitHub / Git provider API limits (HTTP 304 Not Modified does not consume API rate quotas).
/// </summary>
public sealed class ETagReleaseCache
{
    private readonly ConcurrentDictionary<string, CachedReleaseEntry> _entries = new(StringComparer.OrdinalIgnoreCase);

    private static string Key(string owner, string repo) => $"{owner.Trim()}/{repo.Trim()}";

    /// <summary>
    /// Attempts to retrieve cached ETag and release list for the given repository.
    /// </summary>
    public bool TryGet(string owner, string repo, out string? etag, out IReadOnlyList<ReleaseInfo>? releases)
    {
        if (_entries.TryGetValue(Key(owner, repo), out var entry))
        {
            etag = entry.ETag;
            releases = entry.Releases;
            return true;
        }

        etag = null;
        releases = null;
        return false;
    }

    /// <summary>
    /// Stores or updates cached ETag and release list.
    /// </summary>
    public void Update(string owner, string repo, string etag, IReadOnlyList<ReleaseInfo> releases)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(repo);
        ArgumentNullException.ThrowIfNull(etag);
        ArgumentNullException.ThrowIfNull(releases);

        _entries[Key(owner, repo)] = new CachedReleaseEntry(etag, releases, DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Clears the cached entries.
    /// </summary>
    public void Clear()
    {
        _entries.Clear();
    }
}
