namespace AppWatch.Core.Domain;

public sealed record WingetUpgradeRow(WingetPackageUpdate Update, string? SystemVersion)
{
    public string Name => Update.Name;
    public string Id => Update.Id;
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
