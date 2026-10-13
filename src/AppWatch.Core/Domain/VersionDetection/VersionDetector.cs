using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using AppWatch.Core.Domain.Models;

namespace AppWatch.Core.Domain.VersionDetection;

public sealed record DetectedVersion(string Version, VersionSource Source, string? Details = null);

public enum VersionSource
{
    Unknown,
    FileVersionInfo,
    RegistryUninstall,
    Winget,
    Chocolatey,
    Scoop,
    FileNamePattern,
    ExecutableMetadata
}

public static class VersionDetector
{
    private static readonly Regex VersionRegex = new(@"\d+\.\d+(\.\d+)*", RegexOptions.Compiled);

    /// <summary>
    /// Detect installed version of an application using multiple strategies
    /// </summary>
    public static async Task<DetectedVersion?> DetectAsync(TrackedApp app, CancellationToken ct = default)
    {
        // 1. Try executable file version (most reliable for portable apps)
        if (!string.IsNullOrEmpty(app.ExecutableName) && !string.IsNullOrEmpty(app.InstallDirectory))
        {
            var exePath = Path.Combine(app.InstallDirectory, app.ExecutableName);
            var fileVer = TryGetFileVersion(exePath);
            if (fileVer is not null)
                return new DetectedVersion(fileVer, VersionSource.FileVersionInfo, exePath);
        }

        // 2. Try registry uninstall keys (most reliable for installed apps)
        var regVer = TryGetRegistryVersion(app);
        if (regVer is not null)
            return regVer;

        // 3. Try winget
        if (!string.IsNullOrEmpty(app.WingetPackageId))
        {
            var wingetVer = await TryGetWingetVersionAsync(app.WingetPackageId, ct);
            if (wingetVer is not null)
                return new DetectedVersion(wingetVer, VersionSource.Winget, $"winget:{app.WingetPackageId}");
        }

        // 4. Try executable name pattern in install directory
        if (!string.IsNullOrEmpty(app.InstallDirectory) && Directory.Exists(app.InstallDirectory))
        {
            var patternVer = TryDetectFromExeInDirectory(app.InstallDirectory, app.ExecutableName);
            if (patternVer is not null)
                return new DetectedVersion(patternVer, VersionSource.FileNamePattern, app.InstallDirectory);
        }

        return null;
    }

    private static string? TryGetFileVersion(string exePath)
    {
        try
        {
            if (File.Exists(exePath))
            {
                var fvi = FileVersionInfo.GetVersionInfo(exePath);
                return fvi.FileVersion ?? fvi.ProductVersion;
            }
        }
        catch { }
        return null;
    }

    private static DetectedVersion? TryGetRegistryVersion(TrackedApp app)
    {
        try
        {
            var searchNames = new[] { app.Name };
            if (!string.IsNullOrEmpty(app.ExecutableName))
                searchNames = searchNames.Concat([Path.GetFileNameWithoutExtension(app.ExecutableName)!]).ToArray();

            var roots = new[]
            {
                Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
                Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"),
                Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall")
            };

            foreach (var root in roots)
            {
                if (root is null) continue;
                foreach (var subKeyName in root.GetSubKeyNames())
                {
                    using var subKey = root.OpenSubKey(subKeyName);
                    if (subKey is null) continue;

                    var displayName = subKey.GetValue("DisplayName")?.ToString() ?? "";
                    var displayVersion = subKey.GetValue("DisplayVersion")?.ToString() ?? "";
                    var publisher = subKey.GetValue("Publisher")?.ToString() ?? "";

                    if (searchNames.Any(n => displayName.Contains(n, StringComparison.OrdinalIgnoreCase)) &&
                        !string.IsNullOrEmpty(displayVersion) &&
                        VersionRegex.IsMatch(displayVersion))
                    {
                        return new DetectedVersion(displayVersion, VersionSource.RegistryUninstall, $"Registry: {displayName}");
                    }
                }
            }
        }
        catch { }
        return null;
    }

    private static async Task<string?> TryGetWingetVersionAsync(string packageId, CancellationToken ct)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "winget",
                Arguments = $"show \"{packageId}\" --accept-source-agreements",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8
            };
            using var proc = Process.Start(psi);
            if (proc is null) return null;
            var output = await proc.StandardOutput.ReadToEndAsync();
            await proc.WaitForExitAsync(ct);
            
            // Parse winget output for version
            var match = VersionRegex.Match(output);
            return match.Success ? match.Value : null;
        }
        catch { }
        return null;
    }

    private static string? TryDetectFromExeInDirectory(string dir, string? exeName)
    {
        try
        {
            var pattern = exeName ?? "*.exe";
            var files = Directory.GetFiles(dir, pattern, SearchOption.AllDirectories);
            foreach (var file in files.OrderByDescending(f => new FileInfo(f).Length))
            {
                var ver = TryGetFileVersion(file);
                if (ver is not null) return ver;
            }
        }
        catch { }
        return null;
    }
}