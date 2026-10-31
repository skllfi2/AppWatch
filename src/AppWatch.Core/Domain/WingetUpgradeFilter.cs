using System.Text.RegularExpressions;

namespace AppWatch.Core.Domain;

public sealed record WingetUpgradeRow(WingetPackageUpdate Update, string? SystemVersion)
{
    public string Name => CleanName(Update.Name, Update.CurrentVersion, SystemVersion);
    public string Id => Update.Id;

    private static readonly Regex TrailingVersionRegex = new(
        @"\s+(?:v\.?|ver\.?|\()?\d+(\.\d+)+[a-z0-9._-]*\)?$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static string CleanName(string rawName, string? currentVersion, string? systemVersion)
    {
        if (string.IsNullOrWhiteSpace(rawName)) return rawName;

        var candidates = new[] { currentVersion, systemVersion };
        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate)) continue;
            var ver = candidate.Trim();
            if (ver.Length < 2) continue;

            if (rawName.EndsWith(" " + ver, StringComparison.OrdinalIgnoreCase))
                return rawName[..^(ver.Length + 1)].TrimEnd();
            if (rawName.EndsWith(" v" + ver, StringComparison.OrdinalIgnoreCase))
                return rawName[..^(ver.Length + 2)].TrimEnd();
            if (rawName.EndsWith(" v." + ver, StringComparison.OrdinalIgnoreCase))
                return rawName[..^(ver.Length + 3)].TrimEnd();
            if (rawName.EndsWith(" (" + ver + ")", StringComparison.OrdinalIgnoreCase))
                return rawName[..^(ver.Length + 3)].TrimEnd();
        }

        // Pattern-based fallback: strip dotted version numbers (e.g. "App 1.2.3", "Tool v2.0")
        // but preserve edition/year identifiers without dots (e.g. "Visual Studio 2022")
        var match = TrailingVersionRegex.Match(rawName);
        if (match.Success && match.Index > 0)
        {
            var cleaned = rawName[..match.Index].TrimEnd();
            if (!string.IsNullOrWhiteSpace(cleaned))
                return cleaned;
        }

        return rawName;
    }
}

public sealed record WingetUpgradeFilterResult(
    IReadOnlyList<WingetUpgradeRow> Visible,
    IReadOnlyList<WingetUpgradeRow> Hidden);

/// <summary>
/// winget lists every package whose installed version it cannot determine ("Unknown") as
/// upgradable — forever, even right after a successful upgrade. For such rows we resolve the
/// version installed on the system ourselves (registry DisplayVersion, file version info of
/// the installed executable, …) and compare it with the version available in the winget
/// catalog: when the system already has the available version or newer, the row is hidden.
/// Rows whose system version cannot be resolved stay visible (behave like plain winget).
/// </summary>
public static class WingetUpgradeFilter
{
    public delegate string? SystemVersionResolver(string packageDisplayName);

    public static WingetUpgradeFilterResult Filter(
        IReadOnlyList<WingetPackageUpdate> rows, SystemVersionResolver resolveSystemVersion)
    {
        var visible = new List<WingetUpgradeRow>();
        var hidden = new List<WingetUpgradeRow>();

        foreach (var row in rows)
        {
            var systemVersion = resolveSystemVersion(row.Name);

            if (systemVersion is not null
                && VersionParser.TryParse(systemVersion, out var system)
                && system is not null
                && VersionParser.TryParse(row.AvailableVersion, out var available)
                && available is not null
                && system.CompareTo(available) >= 0)
            {
                hidden.Add(new WingetUpgradeRow(row, systemVersion));
                continue;
            }

            visible.Add(new WingetUpgradeRow(row, systemVersion));
        }

        return new WingetUpgradeFilterResult(visible, hidden);
    }
}
