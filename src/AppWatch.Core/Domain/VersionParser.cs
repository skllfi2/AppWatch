using System.Text.RegularExpressions;

namespace AppWatch.Core.Domain;

public sealed record ParsedVersion(long Major, long Minor, long Patch, long Revision = 0, string? PreRelease = null, string? LetterSuffix = null)
    : IComparable<ParsedVersion>
{
    public int CompareTo(ParsedVersion? other) => other is null ? 1 : VersionParser.Compare(this, other);
}

public static class VersionParser
{
    // oct18 E1: Flowseal tags use a bare trailing letter as a patch bump within the
    // same numeric base — live order: 1.9.8 < 1.9.8b < 1.9.8c < 1.9.9a … 1.9.9d < 1.10.0.
    // Keywords like "rc"/"beta" are NOT bare letters — those stay prereleases (dash form).
    private static readonly Regex BareLetterSuffixRegex = new(@"^(\d+)([A-Za-z]+)$", RegexOptions.Compiled);

    // oct18 E2: qbittorrent tags glue a prerelease keyword to the last segment without a
    // dash ("release-5.3.0rc1"). Recognized keywords rank BELOW the stable base — the
    // opposite of Flowseal's bare letters, matching GitHub's prerelease flag on them.
    private static readonly Regex KeywordSuffixRegex =
        new(@"^(\d+)(rc|beta|alpha|preview|pre|dev)(\d*)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // oct18 E3: real-world tag word prefixes, longest first so "release-" wins over "rel/".
    private static readonly string[] TagPrefixes = ["release-", "version-", "build-", "rel/"];

    public static bool TryParse(string? raw, out ParsedVersion? version)
    {
        version = null;
        if (string.IsNullOrWhiteSpace(raw)) return false;

        var s = raw.Trim();
        // oct18 E3: strip word prefixes BEFORE the 'v' trim — "version-0.5.0" starts
        // with 'v' and was previously mangled into "ersion-0.5.0" (unparseable).
        // Formats named in RESEARCH.md D1: "release-", "rel/", "version-", "build-".
        foreach (var prefix in TagPrefixes)
        {
            if (!s.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
            s = s[prefix.Length..];
            break;
        }
        s = s.TrimStart('v', 'V');
        var plus = s.IndexOf('+');
        if (plus >= 0) s = s[..plus];

        string? preRelease = null;
        var dash = s.IndexOf('-');
        if (dash >= 0)
        {
            preRelease = s[(dash + 1)..];
            s = s[..dash];
        }

        var parts = s.Split('.');
        // 1..4 numeric segments: semver tags ("1.2.3") and Windows 4-part versions ("1.19.3.3219")
        if (parts.Length is < 1 or > 4) return false;

        var nums = new long[4];
        string? letterSuffix = null;
        for (var i = 0; i < parts.Length; i++)
        {
            if (long.TryParse(parts[i], out var n))
            {
                nums[i] = n;
                continue;
            }

            // oct18 E2: last segment may carry a glued prerelease keyword ("5.3.0rc1") —
            // checked BEFORE bare letters so "0rc" is a prerelease, not a letter suffix.
            if (i == parts.Length - 1 && TryStripKeywordPreRelease(parts[i], out n, out var keyword))
            {
                nums[i] = n;
                preRelease ??= keyword;
                continue;
            }

            // oct18 E1: only the LAST segment may carry a bare letter suffix ("1.9.9d");
            // anything else non-numeric keeps the whole tag unparseable.
            if (i == parts.Length - 1 && TryStripBareLetterSuffix(parts[i], out n, out letterSuffix))
            {
                nums[i] = n;
                continue;
            }

            return false;
        }

        version = new ParsedVersion(nums[0], nums[1], nums[2], nums[3],
            string.IsNullOrEmpty(preRelease) ? null : preRelease, letterSuffix);
        return true;
    }

    // oct18 E1: "9d" → numeric 9 + suffix "d"; "0rc1" does NOT match (letters must be
    // trailing and pure) so keyword suffixes stay unhandled here.
    private static bool TryStripBareLetterSuffix(string segment, out long number, out string? letterSuffix)
    {
        number = 0;
        letterSuffix = null;
        var match = BareLetterSuffixRegex.Match(segment);
        if (!match.Success) return false;
        if (!long.TryParse(match.Groups[1].Value, out number)) return false;
        letterSuffix = match.Groups[2].Value.ToLowerInvariant();
        return true;
    }

    // oct18 E2: "0rc1" → numeric 0 + PreRelease "rc1"; "0beta1" → "beta1".
    private static bool TryStripKeywordPreRelease(string segment, out long number, out string? keyword)
    {
        number = 0;
        keyword = null;
        var match = KeywordSuffixRegex.Match(segment);
        if (!match.Success) return false;
        if (!long.TryParse(match.Groups[1].Value, out number)) return false;
        keyword = match.Groups[2].Value.ToLowerInvariant() + match.Groups[3].Value;
        return true;
    }

    public static bool IsNewer(string? current, string candidate)
    {
        if (!TryParse(candidate, out var cand) || cand is null) return false;
        if (current is null) return true;
        if (!TryParse(current, out var cur) || cur is null) return true;
        return Compare(cur, cand) < 0;
    }

    public static int Compare(ParsedVersion a, ParsedVersion b)
    {
        var numeric = (a.Major, a.Minor, a.Patch, a.Revision)
            .CompareTo((b.Major, b.Minor, b.Patch, b.Revision));
        if (numeric != 0) return numeric;
        var letter = CompareLetterSuffix(a.LetterSuffix, b.LetterSuffix);
        if (letter != 0) return letter;
        return ComparePreRelease(a.PreRelease, b.PreRelease);
    }

    // oct18 E1: bare trailing letters rank ABOVE the unsuffixed base (live Flowseal
    // order: 1.9.8 < 1.9.8b) and order among themselves alphabetically.
    private static int CompareLetterSuffix(string? a, string? b)
    {
        if (a is null && b is null) return 0;
        if (a is null) return -1;
        if (b is null) return 1;
        return string.CompareOrdinal(a, b);
    }

    private static int ComparePreRelease(string? a, string? b)
    {
        if (a is null && b is null) return 0;
        if (a is null) return 1;
        if (b is null) return -1;

        var pa = a.Split('.');
        var pb = b.Split('.');
        for (var i = 0; i < Math.Max(pa.Length, pb.Length); i++)
        {
            var ai = i < pa.Length ? pa[i] : null;
            var bi = i < pb.Length ? pb[i] : null;
            if (ai is null) return -1;
            if (bi is null) return 1;

            var an = long.TryParse(ai, out var av);
            var bn = long.TryParse(bi, out var bv);
            if (an && bn)
            {
                var c = av.CompareTo(bv);
                if (c != 0) return c;
            }
            else if (an) return -1;
            else if (bn) return 1;
            else
            {
                var c = string.CompareOrdinal(ai, bi);
                if (c != 0) return c;
            }
        }
        return 0;
    }
}
