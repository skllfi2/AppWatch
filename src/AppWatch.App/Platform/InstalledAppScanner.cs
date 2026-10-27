using Microsoft.Win32;
using System.IO;

namespace AppWatch.Platform;

/// <summary>One installed program from the ARP registry (Add/Remove Programs).</summary>
public sealed record InstalledApp(
    string Name,
    string? Version,
    string? Publisher,
    string? InstallDateText)
{
    public string Monogram => string.IsNullOrEmpty(Name) ? "?" : Name[..1].ToUpperInvariant();
    public bool CanLaunch => !string.IsNullOrEmpty(ExecutablePath);

    // Resolved at runtime via InstalledAppItem (which has access to IAppLauncher)
    public string? ExecutablePath { get; init; }

    // Optional: path to icon file (extracted from exe) or package logo
    public string? IconPath { get; init; }

    // For UWP apps: package family name to load logo/thumbnail
    public string? PackageFamilyName { get; init; }
}

/// <summary>
/// Scans the Add/Remove-Programs registry (ARP): HKLM + HKCU, native + WOW6432Node.
/// Pure inventory read — no network, no winget process, completes in milliseconds.
/// </summary>
public static class InstalledAppScanner
{
    private static readonly string[] Roots =
    [
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
        @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall",
    ];

    public static List<InstalledApp> Scan()
    {
        var seen = new Dictionary<string, InstalledApp>(StringComparer.OrdinalIgnoreCase);

        foreach (var (hive, view) in new[]
                 { (RegistryHive.LocalMachine, RegistryView.Registry64), (RegistryHive.CurrentUser, RegistryView.Default) })
        {
            foreach (var rootPath in Roots)
            {
                using var root = RegistryKey.OpenBaseKey(hive, view).OpenSubKey(rootPath);
                if (root is null) continue;

                foreach (var subKeyName in root.GetSubKeyNames())
                {
                    using var key = root.OpenSubKey(subKeyName);
                    if (key is null) continue;

                    // Skip OS components and update packages
                    if (key.GetValue("SystemComponent") is int sc && sc == 1) continue;
                    if (key.GetValue("ParentKeyName") is string) continue;
                    if (key.GetValue("ReleaseType") is string) continue;

                    var name = key.GetValue("DisplayName") as string;
                    if (string.IsNullOrWhiteSpace(name)) continue;

                    var version = key.GetValue("DisplayVersion") as string;
                    var publisher = key.GetValue("Publisher") as string;
                    var installDateText = FormatDate(key.GetValue("InstallDate") as string);

                    // Try to find executable path from registry
                    string? exePath = null;
                    var installLocation = key.GetValue("InstallLocation") as string;
                    if (!string.IsNullOrEmpty(installLocation) && System.IO.Directory.Exists(installLocation))
                    {
                        // Look for .exe files in install location
                        var exes = System.IO.Directory.GetFiles(installLocation, "*.exe", System.IO.SearchOption.TopDirectoryOnly);
                        if (exes.Length > 0)
                        {
                            // Prefer exe matching app name
                            var match = exes.FirstOrDefault(e => 
                                System.IO.Path.GetFileNameWithoutExtension(e).Equals(name, StringComparison.OrdinalIgnoreCase));
                            exePath = match ?? exes[0];
                        }
                    }

                    var app = new InstalledApp(
                                            name.Trim(),
                                            string.IsNullOrWhiteSpace(version) ? null : version.Trim(),
                                            string.IsNullOrWhiteSpace(publisher) ? null : publisher.Trim(),
                                            installDateText)
                                        {
                                            ExecutablePath = exePath,
                                            IconPath = exePath != null ? ExtractIconToTemp(exePath) : null
                                        };

                    // The same app appears in several hives/views — keep the richest entry
                    if (!seen.TryGetValue(app.Name, out var existing)
                        || Richer(app, existing))
                    {
                        seen[app.Name] = app;
                    }
                }
            }
        }

        return seen.Values
            .OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Store/UWP packages (PackageManager) — ARP never lists them, but users expect
    /// them in "Приложения". Skips framework/resource/system packages. Best-effort:
    /// any single-package failure or API denial just yields fewer rows.
    /// </summary>
    public static List<InstalledApp> ScanUwp()
    {
        var list = new List<InstalledApp>();
        try
        {
            var pm = new Windows.Management.Deployment.PackageManager();
            foreach (var pkg in pm.FindPackagesForUser(""))
            {
                try
                {
                    if (pkg.IsFramework || pkg.IsResourcePackage || pkg.IsBundle) continue;
                    if (pkg.SignatureKind == Windows.ApplicationModel.PackageSignatureKind.System) continue;
                    var name = pkg.DisplayName;
                    if (string.IsNullOrWhiteSpace(name)) continue;
                    var v = pkg.Id.Version;
                    var version = $"{v.Major}.{v.Minor}.{v.Build}.{v.Revision}";
                    list.Add(new InstalledApp(
                                            name.Trim(),
                                            version,
                                            string.IsNullOrWhiteSpace(pkg.PublisherDisplayName) ? "Microsoft Store" : pkg.PublisherDisplayName.Trim(),
                                            pkg.InstalledDate.ToLocalTime().ToString("dd.MM.yyyy"))
                                        {
                                            ExecutablePath = null, // UWP launch goes through shell:AppsFolder, not a raw exe
                                            PackageFamilyName = pkg.Id.FamilyName
                                        });
                }
                catch { /* one bad package must not kill the scan */ }
            }
        }
        catch { /* PackageManager denied (unlikely for current user) — ARP list still works */ }
        return list.OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static bool Richer(InstalledApp candidate, InstalledApp existing) =>
        (candidate.Version is not null) switch
        {
            true when existing.Version is null => true,
            _ => candidate.Publisher is not null && existing.Publisher is null,
        };

    private static string? FormatDate(string? yyyymmdd)
        {
            if (string.IsNullOrWhiteSpace(yyyymmdd) || yyyymmdd.Length != 8) return null;
            if (!DateTime.TryParseExact(yyyymmdd, "yyyyMMdd", null,
                    System.Globalization.DateTimeStyles.None, out var date)) return null;
            return date.ToString("dd.MM.yyyy");
        }

        /// <summary>
        /// Placeholder for icon extraction. Currently disabled — we'll add a proper
        /// async converter in the UI layer that uses StorageItemThumbnail.
        /// </summary>
        private static string? ExtractIconToTemp(string exePath) => null;
    }
