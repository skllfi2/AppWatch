namespace AppWatch.Core.Domain;

/// <summary>
/// winget itself crashed while enumerating installed packages (E_INVALIDARG, exit 0x80070057).
/// Known trigger: Microsoft's catalog data for Microsoft.PowerShell has overlapping version
/// ranges, killing `winget list/upgrade/export` for everyone (microsoft/winget-cli#6613,
/// PowerShell/PowerShell#28252) until Microsoft fixes the CDN data or ships a winget patch.
/// Not fixable client-side; scoped commands (`search`, `install --id`, `show`) keep working.
/// </summary>
public sealed class WingetCatalogBrokenException(string output)
    : Exception($"winget crashed while listing updates (catalog data bug): {output}");
