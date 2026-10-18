using AppWatch.Core.Domain.Models;

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
    public static List<CatalogEntry> Merge(
        IReadOnlyList<CatalogEntry> seed,
        IReadOnlyList<CatalogEntry> user)
    {
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

        return result;
    }

    /// <summary>True when this owner/repo pair exists in the user file (not the seed).</summary>
    public static bool IsUserEntry(
        IReadOnlyList<CatalogEntry> userEntries,
        string repoOwner, string repoName) =>
        userEntries.Any(e =>
            string.Equals(e.RepoOwner, repoOwner, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(e.RepoName, repoName, StringComparison.OrdinalIgnoreCase));
}
