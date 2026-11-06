using System.Text.RegularExpressions;

namespace AppWatch.Core.Domain;

/// <summary>
/// Filters out documentation repos, samples, test suites, or user-defined blacklist patterns
/// during Stars import, system discovery, or catalog browsing.
/// </summary>
public static class RepositoryBlacklistFilter
{
    private static readonly string[] DefaultPatterns =
    [
        @"^docs?$",
        @"^documentation$",
        @"-docs?$",
        @"^docs?-",
        @"-documentation$",
        @"^awesome$",
        @"-awesome$",
        @"^awesome-",
        @"-cheatsheet$",
        @"-interview-",
        @"-roadmap$",
        @"-tutorials?$",
        @"-samples?$",
        @"-examples?$"
    ];

    public static bool IsBlacklisted(string? repoOwner, string? repoName, IEnumerable<string>? customPatterns = null)
    {
        if (string.IsNullOrWhiteSpace(repoName)) return false;

        var patterns = customPatterns != null
            ? DefaultPatterns.Concat(customPatterns)
            : DefaultPatterns;

        var full = !string.IsNullOrWhiteSpace(repoOwner) ? $"{repoOwner}/{repoName}" : repoName;

        foreach (var pat in patterns)
        {
            if (string.IsNullOrWhiteSpace(pat)) continue;

            // Simple wildcard support: e.g. *awesome* or *-docs
            if (pat.Contains('*'))
            {
                var regexPattern = "^" + Regex.Escape(pat).Replace("\\*", ".*") + "$";
                if (Regex.IsMatch(repoName, regexPattern, RegexOptions.IgnoreCase) ||
                    Regex.IsMatch(full, regexPattern, RegexOptions.IgnoreCase))
                {
                    return true;
                }
            }
            else
            {
                if (Regex.IsMatch(repoName, pat, RegexOptions.IgnoreCase) ||
                    Regex.IsMatch(full, pat, RegexOptions.IgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
