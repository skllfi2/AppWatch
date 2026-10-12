namespace AppWatch.Infrastructure;

/// <summary>
/// Resolves the version of an application installed on this system by its display name.
/// Chain: Add/Remove Programs registry DisplayVersion → file version of the executable
/// pointed to by InstallLocation. Used for winget packages whose installed version
/// winget cannot determine ("Unknown").
/// </summary>
public sealed class SystemVersionResolver
{
    public string? ResolveInstalledVersion(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName)) return null;

        foreach (var root in Roots())
        {
            using var rootKey = root;
            if (rootKey is null) continue;

            foreach (var subKeyName in rootKey.GetSubKeyNames())
            {
                using var key = rootKey.OpenSubKey(subKeyName);
                if (key?.GetValue("DisplayName") is not string name) continue;
                if (!string.Equals(name.Trim(), displayName.Trim(), StringComparison.OrdinalIgnoreCase)) continue;

                // 1) registry DisplayVersion
                if (key.GetValue("DisplayVersion") is string regVersion
                    && !string.IsNullOrWhiteSpace(regVersion))
                    return regVersion.Trim();

                // 2) file version of the installed executable
                if (key.GetValue("InstallLocation") is string location && !string.IsNullOrWhiteSpace(location))
                {
                    var exeVersion = TryGetExecutableVersion(location.Trim());
                    if (exeVersion is not null) return exeVersion;
                }
            }
        }
        return null;
    }

    private static string? TryGetExecutableVersion(string installLocation)
    {
        try
        {
            if (!Directory.Exists(installLocation)) return null;
            // look for a matching or the single exe in the install dir
            var exes = Directory.EnumerateFiles(installLocation, "*.exe", SearchOption.TopDirectoryOnly);
            foreach (var exe in exes)
            {
                var name = Path.GetFileNameWithoutExtension(exe);
                if (name.Contains("unins", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("uninstall", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("helper", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("update", StringComparison.OrdinalIgnoreCase))
                    continue;
                var vi = global::System.Diagnostics.FileVersionInfo.GetVersionInfo(exe);
                var product = vi.ProductVersion?.Trim();
                if (!string.IsNullOrWhiteSpace(product)) return product;
            }
        }
        catch
        {
            // access denied / missing files — treat as unresolvable
        }
        return null;
    }

    private static IEnumerable<Microsoft.Win32.RegistryKey?> Roots()
    {
        yield return Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
        yield return Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall");
        yield return Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
    }
}
