using System.Runtime.Versioning;
using Microsoft.Win32;

namespace AppWatch.Core.Domain;

/// <summary>
/// Manages CLI shim generation and user %PATH% registration.
/// Ensures any CLI-based tool tracked by AppWatch is immediately executable from any terminal.
/// </summary>
public static class ShimService
{
    /// <summary>
    /// Directory where CLI shim wrappers (.cmd, symlinks) are placed.
    /// </summary>
    public static string ShimsDirectory => Path.Combine(AppPaths.DataRoot, "shims");

    /// <summary>
    /// Ensures the shims directory exists on disk.
    /// </summary>
    public static void EnsureDirectoryExists()
    {
        Directory.CreateDirectory(ShimsDirectory);
    }

    /// <summary>
    /// Writes a lightweight .cmd wrapper script forwarding all arguments to target executable.
    /// </summary>
    public static void WriteCmdShimFile(string shimPath, string targetExePath)
    {
        ArgumentNullException.ThrowIfNull(shimPath);
        ArgumentNullException.ThrowIfNull(targetExePath);

        var dir = Path.GetDirectoryName(shimPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var script = $"@echo off\r\n\"{targetExePath}\" %*\r\n";
        File.WriteAllText(shimPath, script);
    }

    /// <summary>
    /// Creates or updates a CLI shim in %ShimsDirectory% for the specified executable.
    /// </summary>
    public static string CreateShim(string commandName, string targetExecutablePath)
    {
        ArgumentNullException.ThrowIfNull(commandName);
        ArgumentNullException.ThrowIfNull(targetExecutablePath);

        EnsureDirectoryExists();

        var baseName = Path.GetFileNameWithoutExtension(commandName);
        var shimPath = Path.Combine(ShimsDirectory, baseName + ".cmd");

        WriteCmdShimFile(shimPath, targetExecutablePath);
        return shimPath;
    }

    /// <summary>
    /// Registers the shims directory into the current user's persistent PATH environment variable.
    /// Safe & idempotent: does nothing if already present.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static bool EnsureInUserPath()
    {
        try
        {
            var shimsDir = Path.GetFullPath(ShimsDirectory).TrimEnd('\\', '/');
            using var key = Registry.CurrentUser.OpenSubKey("Environment", writable: true);
            if (key is null) return false;

            var currentPath = key.GetValue("Path", "", RegistryValueOptions.DoNotExpandEnvironmentNames) as string ?? "";
            var segments = currentPath.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (segments.Any(s => string.Equals(s.TrimEnd('\\', '/'), shimsDir, StringComparison.OrdinalIgnoreCase)))
            {
                return true; // Already registered
            }

            var newPath = string.IsNullOrEmpty(currentPath) ? shimsDir : currentPath + ";" + shimsDir;
            key.SetValue("Path", newPath, RegistryValueKind.ExpandString);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
