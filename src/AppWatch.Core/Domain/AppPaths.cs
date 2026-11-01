namespace AppWatch.Core.Domain;

/// <summary>
/// Centralized management of application filesystem directories.
/// Enforces clean separation between application binaries (managed by Velopack in %LocalAppData%\AppWatch)
/// and user data/apps (isolated in %LocalAppData%\AppWatchData).
/// </summary>
public static class AppPaths
{
    private static readonly string LocalAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    private static readonly string RoamingAppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

    /// <summary>
    /// Root directory for all user-managed data (downloaded portable apps, version cache, logs).
    /// Kept strictly separate from %LocalAppData%\AppWatch so that installer updates/repairs never wipe user data.
    /// </summary>
    public static string DataRoot => Path.Combine(LocalAppData, "AppWatchData");

    /// <summary>Default directory where downloaded applications are staged and installed.</summary>
    public static string DefaultInstallRoot => Path.Combine(DataRoot, "Apps");

    /// <summary>Directory where historical version snapshots are cached for rollback.</summary>
    public static string DefaultVersionsRoot => Path.Combine(DataRoot, "Versions");

    /// <summary>Directory where application and crash diagnostic logs are recorded.</summary>
    public static string DefaultLogsRoot => Path.Combine(DataRoot, "logs");

    /// <summary>Roaming directory where configuration is stored.</summary>
    public static string ConfigDirectory => Path.Combine(RoamingAppData, "AppWatch");

    /// <summary>Legacy install directory (%LocalAppData%\AppWatch\Apps) used prior to v0.2.2.</summary>
    public static string LegacyInstallRoot => Path.Combine(LocalAppData, "AppWatch", "Apps");

    /// <summary>Legacy version cache directory (%LocalAppData%\AppWatch\Versions).</summary>
    public static string LegacyVersionsRoot => Path.Combine(LocalAppData, "AppWatch", "Versions");

    /// <summary>Legacy logs directory (%LocalAppData%\AppWatch\logs).</summary>
    public static string LegacyLogsRoot => Path.Combine(LocalAppData, "AppWatch", "logs");

    /// <summary>
    /// Resolves the effective install root for downloaded apps.
    /// Prefers user-configured path, then safely defaults to DefaultInstallRoot.
    /// </summary>
    /// <summary>
    /// Remaps any path referencing the legacy directory (%LocalAppData%\AppWatch\Apps\...)
    /// to the new safe directory (%LocalAppData%\AppWatchData\Apps\...).
    /// </summary>
    public static string? RemapLegacyPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return path;

        var fullLegacy = Path.GetFullPath(LegacyInstallRoot).TrimEnd('\\', '/');
        var fullPath = Path.GetFullPath(path).TrimEnd('\\', '/');

        if (string.Equals(fullPath, fullLegacy, StringComparison.OrdinalIgnoreCase))
            return DefaultInstallRoot;

        if (fullPath.StartsWith(fullLegacy + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            var relative = fullPath.Substring(fullLegacy.Length + 1);
            return Path.Combine(DefaultInstallRoot, relative);
        }

        return path;
    }

    public static string ResolveInstallRoot(string? configuredPath)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
            return DefaultInstallRoot;

        // If configured path points to legacy directory, redirect to new safe directory
        if (string.Equals(Path.GetFullPath(configuredPath).TrimEnd('\\', '/'),
                          Path.GetFullPath(LegacyInstallRoot).TrimEnd('\\', '/'),
                          StringComparison.OrdinalIgnoreCase))
        {
            return DefaultInstallRoot;
        }

        return configuredPath;
    }

    /// <summary>
    /// Performs one-time seamless migration of user data from the legacy (%LocalAppData%\AppWatch\Apps)
    /// directory to the isolated (%LocalAppData%\AppWatchData\Apps) directory if legacy exists and target does not.
    /// </summary>
    public static void MigrateLegacyDataIfNeeded()
    {
        try
        {
            if (!Directory.Exists(DefaultInstallRoot) && Directory.Exists(LegacyInstallRoot))
            {
                Directory.CreateDirectory(DataRoot);
                MigrateDirectorySafe(LegacyInstallRoot, DefaultInstallRoot);
            }

            if (!Directory.Exists(DefaultVersionsRoot) && Directory.Exists(LegacyVersionsRoot))
            {
                Directory.CreateDirectory(DataRoot);
                MigrateDirectorySafe(LegacyVersionsRoot, DefaultVersionsRoot);
            }
        }
        catch
        {
            // Best effort migration - never crash app startup
        }
    }

    private static void MigrateDirectorySafe(string sourceDir, string targetDir)
    {
        try
        {
            Directory.CreateDirectory(targetDir);
            foreach (var file in Directory.GetFiles(sourceDir))
            {
                var destFile = Path.Combine(targetDir, Path.GetFileName(file));
                try
                {
                    if (!File.Exists(destFile))
                    {
                        File.Copy(file, destFile, overwrite: true);
                        File.Delete(file);
                    }
                }
                catch
                {
                    // If file is locked, copy may succeed but delete will fail - tolerate safely
                }
            }

            foreach (var subDir in Directory.GetDirectories(sourceDir))
            {
                var dirName = Path.GetFileName(subDir);
                var destSubDir = Path.Combine(targetDir, dirName);
                MigrateDirectorySafe(subDir, destSubDir);
                try { Directory.Delete(subDir, recursive: false); } catch { }
            }
        }
        catch
        {
            // Best effort
        }
    }
}
