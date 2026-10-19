namespace AppWatch.Core.Domain;

/// <summary>
/// D3 (oct18): fuzzy catalog search — pure, hermetic, culture-invariant scoring so
/// partial names and typos still surface entries ("ytdl" → "yt-dlp", "vescop" →
/// "Vesktop"). Contract: 0 = no match; otherwise the highest tier wins:
/// exact > prefix > substring > typo (Levenshtein ≤ 2) > subsequence > description.
/// Tie-breaks (score, then name) belong to the caller ranking a list.
/// </summary>
public static class CatalogSearch
{
    public const int ScoreExact = 100;
    public const int ScorePrefix = 90;
    public const int ScoreSubstring = 80;
    public const int ScoreTypo = 60;
    public const int ScoreSubsequence = 50;
    public const int ScoreDescription = 40;

    // Short queries are too noisy for fuzzy tiers: a 1–2 char "typo"/subsequence
    // matches almost everything, so those tiers require at least 3 characters.
    private const int MinFuzzyQueryLength = 3;

    public static int Score(string? query, string? name, string? description)
    {
        if (string.IsNullOrWhiteSpace(query)) return 0;

        var q = query.Trim().ToLowerInvariant();
        var n = (name ?? "").Trim().ToLowerInvariant();

        if (n.Length > 0)
        {
            if (string.Equals(n, q, StringComparison.Ordinal)) return ScoreExact;
            if (n.StartsWith(q, StringComparison.Ordinal)) return ScorePrefix;
            if (n.Contains(q, StringComparison.Ordinal)) return ScoreSubstring;
            if (q.Length >= MinFuzzyQueryLength)
            {
                if (Levenshtein(q, n) <= 2) return ScoreTypo;
                if (IsSubsequence(q, n)) return ScoreSubsequence;
            }
        }

        var d = (description ?? "").ToLowerInvariant();
        return d.Contains(q, StringComparison.Ordinal) ? ScoreDescription : 0;
    }

    /// <summary>Classic two-row Levenshtein edit distance (insert/delete/substitute).</summary>
    private static int Levenshtein(string a, string b)
    {
        if (a == b) return 0;
        if (a.Length == 0) return b.Length;
        if (b.Length == 0) return a.Length;

        var prev = new int[b.Length + 1];
        var curr = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++) prev[j] = j;

        for (var i = 1; i <= a.Length; i++)
        {
            curr[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                curr[j] = Math.Min(Math.Min(curr[j - 1] + 1, prev[j] + 1), prev[j - 1] + cost);
            }
            (prev, curr) = (curr, prev);
        }
        return prev[b.Length];
    }

    private static bool IsSubsequence(string needle, string haystack)
    {
        var i = 0;
        foreach (var c in haystack)
        {
            if (i < needle.Length && needle[i] == c) i++;
            if (i == needle.Length) return true;
        }
        return i == needle.Length;
    }
}
