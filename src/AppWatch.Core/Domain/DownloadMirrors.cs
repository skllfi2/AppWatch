namespace AppWatch.Core.Domain;

/// <summary>
/// B8: formats fallback mirror download URLs when direct github.com downloads fail.
/// Mirrors (ghproxy / ghfast) expect the FULL original URL appended to the mirror host,
/// e.g. "https://ghfast.top/https://github.com/owner/repo/releases/download/v1.0/app.zip".
/// </summary>
public static class DownloadMirrors
{
    public static IReadOnlyList<string> Candidates(string originalUrl, IEnumerable<string>? mirrorPrefixes)
    {
        var candidates = new List<string> { originalUrl };
        if (!IsMirrorable(originalUrl) || mirrorPrefixes is null)
            return candidates;

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var prefix in mirrorPrefixes)
        {
            if (string.IsNullOrWhiteSpace(prefix)) continue;
            var trimmed = prefix.TrimEnd('/') + "/";
            if (seen.Add(trimmed))
            {
                candidates.Add(trimmed + originalUrl);
            }
        }
        return candidates;
    }

    /// <summary>Only github.com release-asset URLs get mirrored (api.github.com and other hosts pass through).</summary>
    public static bool IsMirrorable(string url) =>
        !string.IsNullOrEmpty(url)
        && url.StartsWith("https://github.com/", StringComparison.OrdinalIgnoreCase)
        && url.Contains("/releases/download/", StringComparison.OrdinalIgnoreCase);

    public static IReadOnlyList<string> Candidates(Uri originalUrl, IEnumerable<string>? mirrorPrefixes) =>
        originalUrl is null ? [] : Candidates(originalUrl.ToString(), mirrorPrefixes);
}
