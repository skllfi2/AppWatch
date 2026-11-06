namespace AppWatch.Core.Domain;

/// <summary>
/// Centralized management of application filesystem directories.
/// Enforces clean separation between application binaries and user data/apps.
/// Supports both installed mode (%LocalAppData%\AppWatchData) and True Portable Mode (via portable.flag).
/// </summary>
public static class AppPaths
{
    private static readonly string StandardLocalAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    private static readonly string StandardRoamingAppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

    private static string? _portableBaseDirectory;

    /// <summary>
    /// Indicates whether AppWatch is currently running in self-contained Portable Mode.
    /// </summary>
    public static bool IsPortableMode => !string.IsNullOrEmpty(_portableBaseDirectory);

    /// <summary>
    /// Checks for portable.flag in the specified directory (defaults to AppContext.BaseDirectory)
    /// and switches AppPaths to portable mode if present.
    /// </summary>
    public static void InitializePortableMode(string? baseDir = null)
    {
        var targetDir = baseDir ?? AppContext.BaseDirectory;
        if (!string.IsNullOrEmpty(targetDir) && File.Exists(Path.Combine(targetDir, "portable.flag")))
        {
            _portableBaseDirectory = Path.GetFullPath(targetDir);
        }
    }

    /// <summary>
    /// Resets portable mode back to standard system paths (primarily for testing).
    /// </summary>
    public static void ResetPortableMode()
    {
        _portableBaseDirectory = null;
    }

    /// <summary>
    /// Root directory for all user-managed data (downloaded portable apps, version cache, logs).
    /// </summary>
    public static string DataRoot => _portableBaseDirectory is not null
        ? Path.Combine(_portableBaseDirectory, "Data")
        : Path.Combine(StandardLocalAppData, "AppWatchData");

    /// <summary>Default directory where downloaded applications are staged and installed.</summary>
    public static string DefaultInstallRoot => Path.Combine(DataRoot, "Apps");

    /// <summary>Directory where historical version snapshots are cached for rollback.</summary>
    public static string DefaultVersionsRoot => Path.Combine(DataRoot, "Versions");

    /// <summary>Directory where application and crash diagnostic logs are recorded.</summary>
    public static string DefaultLogsRoot => Path.Combine(DataRoot, "logs");

    /// <summary>Directory where configuration is stored.</summary>
    public static string ConfigDirectory => _portableBaseDirectory is not null
        ? Path.Combine(_portableBaseDirectory, "Data", "Config")
        : Path.Combine(StandardRoamingAppData, "AppWatch");

    /// <summary>Legacy install directory (%LocalAppData%\AppWatch\Apps) used prior to v0.2.2.</summary>
    public static string LegacyInstallRoot => Path.Combine(StandardLocalAppData, "AppWatch", "Apps");

    /// <summary>Legacy version cache directory (%LocalAppData%\AppWatch\Versions).</summary>
    public static string LegacyVersionsRoot => Path.Combine(StandardLocalAppData, "AppWatch", "Versions");

    /// <summary>Legacy logs directory (%LocalAppData%\AppWatch\logs).</summary>
    public static string LegacyLogsRoot => Path.Combine(StandardLocalAppData, "AppWatch", "logs");

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

    /// <summary>
    /// Resolves the effective install root for downloaded apps.
    /// Prefers user-configured path, then safely defaults to DefaultInstallRoot.
    /// </summary>
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
