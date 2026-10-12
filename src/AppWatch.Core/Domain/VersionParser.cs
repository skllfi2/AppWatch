namespace AppWatch.Core.Domain;

public sealed record ParsedVersion(long Major, long Minor, long Patch, long Revision = 0, string? PreRelease = null)
    : IComparable<ParsedVersion>
{
    public int CompareTo(ParsedVersion? other) => other is null ? 1 : VersionParser.Compare(this, other);
}

public static class VersionParser
{
    public static bool TryParse(string? raw, out ParsedVersion? version)
    {
        version = null;
        if (string.IsNullOrWhiteSpace(raw)) return false;

        var s = raw.Trim().TrimStart('v', 'V');
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
        for (var i = 0; i < parts.Length; i++)
        {
            if (!long.TryParse(parts[i], out var n)) return false;
            nums[i] = n;
        }

        version = new ParsedVersion(nums[0], nums[1], nums[2], nums[3],
            string.IsNullOrEmpty(preRelease) ? null : preRelease);
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
        return numeric != 0 ? numeric : ComparePreRelease(a.PreRelease, b.PreRelease);
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
