using AppWatch.Core.Domain.Models;
using System.Collections.ObjectModel;

namespace AppWatch.Core.Domain;

/// <summary>
/// Pure catalog merge: user entries override seed entries by owner/repo.
/// Extracted for testability — CatalogService (App) does the IO, this does the logic.
/// </summary>
public static class CatalogMerger
{
    /// <summary>
    /// Merge seed + user entries. User entries with the same owner/repo as a seed entry
    /// replace it (user's config wins). User-only entries are appended at the end.
    /// Order: seed order preserved, user-only entries appended alphabetically.
    /// </summary>
    public static IReadOnlyList<CatalogEntry> Merge(
        IReadOnlyList<CatalogEntry> seed,
        IReadOnlyList<CatalogEntry> user)
    {
        ArgumentNullException.ThrowIfNull(seed);
        ArgumentNullException.ThrowIfNull(user);
        var userByKey = new Dictionary<string, CatalogEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in user)
            userByKey[$"{e.RepoOwner}/{e.RepoName}"] = e;

        var result = new List<CatalogEntry>(seed.Count + user.Count);
        var usedUserKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // seed entries, replaced by user overrides where present
        foreach (var entry in seed)
        {
            var key = $"{entry.RepoOwner}/{entry.RepoName}";
            if (userByKey.TryGetValue(key, out var userEntry))
            {
                result.Add(userEntry);
                usedUserKeys.Add(key);
            }
            else
            {
                result.Add(entry);
            }
        }

        // user-only entries appended alphabetically
        var remaining = user
            .Where(e => !usedUserKeys.Contains($"{e.RepoOwner}/{e.RepoName}"))
            .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        result.AddRange(remaining);

        return new ReadOnlyCollection<CatalogEntry>(result);
    }

    /// <summary>True when this owner/repo pair exists in the user file (not the seed).</summary>
    public static bool IsUserEntry(
        IReadOnlyList<CatalogEntry> userEntries,
        string repoOwner, string repoName)
    {
        ArgumentNullException.ThrowIfNull(userEntries);
        ArgumentNullException.ThrowIfNull(repoOwner);
        ArgumentNullException.ThrowIfNull(repoName);
        return userEntries.Any(e =>
            string.Equals(e.RepoOwner, repoOwner, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(e.RepoName, repoName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Multi-source merge: an ordered list of (sourceName, entries). Later sources override
    /// earlier ones by owner/repo. Returns merged entries + a repo→source lookup for UI badges.
    /// </summary>
    public static (IReadOnlyList<CatalogEntry> Entries, IReadOnlyDictionary<string, string> SourceByRepo) MergeSources(
        IReadOnlyList<(string SourceName, IReadOnlyList<CatalogEntry> Entries)> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);
        // build final map: later sources win
        var byRepo = new Dictionary<string, (CatalogEntry Entry, string Source)>(StringComparer.OrdinalIgnoreCase);
        foreach (var (sourceName, entries) in sources)
        {
            ArgumentNullException.ThrowIfNull(sourceName);
            ArgumentNullException.ThrowIfNull(entries);
            foreach (var e in entries)
                byRepo[$"{e.RepoOwner}/{e.RepoName}"] = (e, sourceName);
        }

        // preserve insertion order: first occurrence position wins
        var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var ordered = new List<CatalogEntry>();
        var sourceByRepo = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (sourceName, entries) in sources)
        {
            foreach (var e in entries)
            {
                var key = $"{e.RepoOwner}/{e.RepoName}";
                if (seenKeys.Add(key))
                {
                    ordered.Add(byRepo[key].Entry);
                    sourceByRepo[key] = byRepo[key].Source;
                }
            }
        }

        return (new ReadOnlyCollection<CatalogEntry>(ordered), new ReadOnlyDictionary<string, string>(sourceByRepo));
    }
}
