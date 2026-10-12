using System.Text.RegularExpressions;

namespace AppWatch.Core.Domain;

public sealed record WingetPackageUpdate(string Name, string Id, string CurrentVersion, string AvailableVersion);

public static class WingetOutputParser
{
    private static readonly Regex Percent = new(@"(\d{1,3})\s*%");
    private static readonly Regex IdPattern = new(@"^[A-Za-z0-9][A-Za-z0-9._+-]*$");
    private static readonly Regex VersionPattern = new(@"^(Unknown|[0-9][0-9A-Za-z.+-]*)$");

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

    /// <summary>Extracts a percentage from a winget progress line; null when the line has none.</summary>
    public static double? TryParseProgressPercent(string line) =>
        Percent.Match(line.Replace("\0", string.Empty)) is { Success: true } match
        && double.TryParse(match.Groups[1].Value, out var value)
        && value is >= 0 and <= 100
            ? value
            : null;
}
