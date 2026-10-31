namespace AppWatch.Core.Domain;

/// <summary>
/// B8 — download mirror rotation policy. ghproxy-style mirrors serve a GitHub URL
/// as a path suffix (https://mirror/https://github.com/...), which unblocks release
/// downloads in networks where github.com is throttled or cut off (RU users).
/// Only release-asset URLs are mirrored; API calls pass through unchanged.
/// </summary>
public static class DownloadMirrors
{
    /// <summary>
    /// Produce download candidates: the original URL first, then one per mirror prefix.
    /// Non-GitHub-release URLs are returned as a single candidate (nothing to mirror).
    /// </summary>
    public static IReadOnlyList<string> Candidates(string originalUrl, IEnumerable<string>? mirrorPrefixes)
    {
        if (!IsMirrorable(originalUrl))
            return [originalUrl];

        var candidates = new List<string> { originalUrl };
        foreach (var raw in mirrorPrefixes ?? [])
        {
            var prefix = raw.Trim();
            if (prefix.Length == 0) continue;
            prefix = prefix.TrimEnd('/');

            var candidate = $"{prefix}/{originalUrl}";
            if (!candidates.Contains(candidate, StringComparer.OrdinalIgnoreCase))
                candidates.Add(candidate);
        }
        return candidates;
    }

    /// <summary>Only github.com release-asset URLs get mirrored (api.github.com and other hosts pass through).</summary>
    public static bool IsMirrorable(string url) =>
        url.StartsWith("https://github.com/", StringComparison.OrdinalIgnoreCase)
        && url.Contains("/releases/download/", StringComparison.OrdinalIgnoreCase);

    public static IReadOnlyList<string> Candidates(Uri originalUrl, IEnumerable<string>? mirrorPrefixes)
    {
        throw new NotImplementedException();
    }
}
