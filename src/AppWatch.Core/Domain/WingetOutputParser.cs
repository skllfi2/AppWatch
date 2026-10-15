using System.Text.RegularExpressions;

namespace AppWatch.Core.Domain;

public sealed record WingetPackageUpdate(string Name, string Id, string CurrentVersion, string AvailableVersion);

public sealed record WingetSearchResult(string Name, string Id, string Version, string Source);

public static class WingetOutputParser
{
    private static readonly Regex Percent = new(@"(\d{1,3})\s*%");
    private static readonly Regex IdPattern = new(@"^[A-Za-z0-9][A-Za-z0-9._+-]*$");
    private static readonly Regex VersionPattern = new(@"^(Unknown|[0-9][0-9A-Za-z.+-]*)$");
    private static readonly Regex SourcePattern = new(@"^[A-Za-z][A-Za-z0-9.-]*$");

    /// <summary>
    /// Parses the table of `winget upgrade` output. Locale-independent: no reliance on
    /// localized headers or column widths. A data row is recognized structurally:
    /// tokens = whitespace split; the last two tokens are the current and available
    /// versions, the token before them is the package Id (never contains spaces),
    /// everything before is the display name (may contain single spaces — winget pads
    /// columns to the widest entry, so a long name leaves only one space before the Id).
    /// Rows failing the Id/version shape checks (error text, summary lines) are skipped.
    /// </summary>
    public static IReadOnlyList<WingetPackageUpdate> ParseUpgrades(string output)
    {
        // winget sometimes emits UTF-16LE that arrives NUL-separated when decoded as UTF-8
        var text = output.Replace("\0", string.Empty);
        var results = new List<WingetPackageUpdate>();
        var tableStarted = false;
        var headerColumns = 0;

        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (line.Trim().Length == 0) continue;

            if (!tableStarted)
            {
                if (line.Contains("---"))
                {
                    tableStarted = true;
                }
                else
                {
                    headerColumns = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
                }
                continue;
            }

            var parsed = ParseRow(line, hasSourceColumn: headerColumns >= 5);
            if (parsed is not null)
                results.Add(parsed);
        }
        return results;
    }

    private static WingetPackageUpdate? ParseRow(string line, bool hasSourceColumn)
    {
        var tokens = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (hasSourceColumn && tokens.Length >= 5)
            tokens = tokens[..^1];
        if (tokens.Length < 4)
            return null;

        var id = tokens[^3];
        var current = tokens[^2];
        var available = tokens[^1];
        var name = string.Join(' ', tokens[..^3]);

        if (name.Length == 0 || !IdPattern.IsMatch(id))
            return null;
        if (!VersionPattern.IsMatch(current) || !VersionPattern.IsMatch(available))
            return null;

        return new WingetPackageUpdate(name, id, current, available);
    }

    /// <summary>
    /// Parses the table of `winget search` output. Locale-independent, structural:
    /// the source is the last token; the Id is the FIRST id-shaped token scanning from
    /// the left that is followed by a version-shaped token, then either directly the
    /// source (4-column name-match table) or a "Tag:"-style match marker (5-column
    /// table). Everything before the Id anchor is the display name (may contain
    /// spaces); everything between the version and the source is the match column and
    /// is ignored. Rows that don't fit either shape (preamble, progress, summary,
    /// error text) are skipped.
    /// </summary>
    public static IReadOnlyList<WingetSearchResult> ParseSearch(string output)
    {
        var text = output.Replace("\0", string.Empty);
        var results = new List<WingetSearchResult>();
        var tableStarted = false;

        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (line.Trim().Length == 0) continue;

            if (!tableStarted)
            {
                if (line.Contains("---")) tableStarted = true;
                continue;
            }

            var parsed = ParseSearchRow(line);
            if (parsed is not null)
                results.Add(parsed);
        }
        return results;
    }

    private static WingetSearchResult? ParseSearchRow(string line)
    {
        var tokens = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length < 4) return null;
        if (!SourcePattern.IsMatch(tokens[^1])) return null;

        for (var i = 1; i < tokens.Length - 2; i++)
        {
            if (!IdPattern.IsMatch(tokens[i])) continue;
            if (!VersionPattern.IsMatch(tokens[i + 1])) continue;

            // 4-column shape: [name..., id, version, source]
            if (i + 2 == tokens.Length - 1)
                return Build(i);
            // 5-column shape: [name..., id, version, "Tag:"/“Moniker:”, match-value..., source]
            if (tokens[i + 2].EndsWith(':'))
                return Build(i);
            // plausible Id but wrong shape — keep scanning (multi-word names)
        }
        return null;

        WingetSearchResult Build(int idIndex) =>
            new(
                string.Join(' ', tokens[..idIndex]),
                tokens[idIndex],
                tokens[idIndex + 1],
                tokens[^1]);
    }

    /// <summary>Single-line squash of a winget output for error messages: strips
    /// NULs/blank lines and keeps the tail of the meaningful text.</summary>
    public static string Squash(string output)
    {
        var text = output.Replace("\0", string.Empty);
        var meaningful = text.Split('\n')
            .Select(l => l.TrimEnd('\r').Trim())
            .Where(l => l.Length > 0 && !l.Contains("---") && !l.Contains('%'))
            .ToArray();
        return meaningful.Length == 0
            ? string.Empty
            : meaningful[^1].Length > 200 ? meaningful[^1][^200..] : meaningful[^1];
    }

    /// <summary>Extracts a percentage from a winget progress line; null when the line has none.</summary>
    public static double? TryParseProgressPercent(string line) =>
        Percent.Match(line.Replace("\0", string.Empty)) is { Success: true } match
        && double.TryParse(match.Groups[1].Value, out var value)
        && value is >= 0 and <= 100
            ? value
            : null;
}
