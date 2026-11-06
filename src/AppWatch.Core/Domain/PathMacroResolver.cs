namespace AppWatch.Core.Domain;

/// <summary>
/// Handles portable path macro expansion and contraction (%APPS%, %USERPROFILE%, %LOCALAPPDATA%, %SYSTEMDRIVE%).
/// Ensures tracked app directories remain valid across PC migrations, user renames, and portable mode.
/// </summary>
public static class PathMacroResolver
{
    public const string AppsMacro = "%APPS%";
    public const string UserProfileMacro = "%USERPROFILE%";
    public const string LocalAppDataMacro = "%LOCALAPPDATA%";
    public const string SystemDriveMacro = "%SYSTEMDRIVE%";

    /// <summary>
    /// Expands path macros into full filesystem paths.
    /// Safely tolerates null, empty strings, and standard paths without macros.
    /// </summary>
    public static string? Expand(string? path, string? customAppsRoot = null)
    {
        if (string.IsNullOrWhiteSpace(path)) return path;

        var appsRoot = AppPaths.ResolveInstallRoot(customAppsRoot).TrimEnd('\\', '/');
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile).TrimEnd('\\', '/');
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData).TrimEnd('\\', '/');
        var systemDrive = (Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\").TrimEnd('\\', '/');

        var expanded = path;

        if (expanded.StartsWith(AppsMacro, StringComparison.OrdinalIgnoreCase))
        {
            expanded = string.Concat(appsRoot, expanded.AsSpan(AppsMacro.Length));
        }

        if (expanded.StartsWith(UserProfileMacro, StringComparison.OrdinalIgnoreCase))
        {
            expanded = string.Concat(userProfile, expanded.AsSpan(UserProfileMacro.Length));
        }

        if (expanded.StartsWith(LocalAppDataMacro, StringComparison.OrdinalIgnoreCase))
        {
            expanded = string.Concat(localAppData, expanded.AsSpan(LocalAppDataMacro.Length));
        }

        if (expanded.StartsWith(SystemDriveMacro, StringComparison.OrdinalIgnoreCase))
        {
            expanded = string.Concat(systemDrive, expanded.AsSpan(SystemDriveMacro.Length));
        }

        // Expand any other standard environment variables if present (e.g. %ProgramFiles%)
        return Environment.ExpandEnvironmentVariables(expanded);
    }

    /// <summary>
    /// Contracts an absolute path into a portable macro path if it falls under known roots (%APPS%, %USERPROFILE%, etc.).
    /// </summary>
    public static string? Contract(string? path, string? customAppsRoot = null)
    {
        if (string.IsNullOrWhiteSpace(path)) return path;

        var fullPath = Path.GetFullPath(path).TrimEnd('\\', '/');
        var appsRoot = Path.GetFullPath(AppPaths.ResolveInstallRoot(customAppsRoot)).TrimEnd('\\', '/');

        // Check if path is within AppsRoot
        if (string.Equals(fullPath, appsRoot, StringComparison.OrdinalIgnoreCase))
            return AppsMacro;

        if (fullPath.StartsWith(appsRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            return string.Concat(AppsMacro, fullPath.AsSpan(appsRoot.Length));
        }

        // Check LocalAppData
        var localAppData = Path.GetFullPath(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)).TrimEnd('\\', '/');
        if (string.Equals(fullPath, localAppData, StringComparison.OrdinalIgnoreCase))
            return LocalAppDataMacro;

        if (fullPath.StartsWith(localAppData + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            return string.Concat(LocalAppDataMacro, fullPath.AsSpan(localAppData.Length));
        }

        // Check UserProfile
        var userProfile = Path.GetFullPath(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)).TrimEnd('\\', '/');
        if (string.Equals(fullPath, userProfile, StringComparison.OrdinalIgnoreCase))
            return UserProfileMacro;

        if (fullPath.StartsWith(userProfile + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            return string.Concat(UserProfileMacro, fullPath.AsSpan(userProfile.Length));
        }

        return path;
    }
}
