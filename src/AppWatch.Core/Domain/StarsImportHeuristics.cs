using AppWatch.Core.Domain.Models;

namespace AppWatch.Core.Domain;

/// <summary>
/// GitHub Stars → CatalogEntry auto-detection: given a repo's release asset file names,
/// determine the right UpdateKind and executable name. Pure, testable.
/// </summary>
public static class StarsImportHeuristics
{
    private static readonly string[] WindowsMarkers = ["win", "windows", "x64", "x86_64", "amd64", "msvc", "pc-windows"];
    private static readonly string[] NonWindowsMarkers = ["mac", "darwin", "osx", "linux", "ubuntu", "debian", "appimage", "dmg", "deb", "rpm", "snap", "flatpak", "android", "apk", "jar", "ios"];
    private static readonly string[] InstallerMarkers = ["setup", "install", "installer", "msi"];
    private static readonly string[] ArchiveExtensions = [".zip", ".7z", ".tar.gz", ".tgz", ".tar"];
    private static readonly string[] ExeExtensions = [".exe", ".msi"];

    /// <summary>
    /// Given release asset file names, suggest the best UpdateKind for a Windows user.
    /// Returns null when no Windows-releasable asset is found.
    /// </summary>
    public static UpdateKind? SuggestUpdateKind(IReadOnlyList<string> assetNames)
    {
        if (assetNames.Count == 0) return null;

        var windowsAssets = assetNames.Where(IsWindowsAsset).ToList();
        if (windowsAssets.Count == 0) return null;

        // Prefer portable zip over single exe over installer (safest first)
        var hasArchive = windowsAssets.Any(a => ArchiveExtensions.Any(a.EndsWith));
        if (hasArchive) return UpdateKind.PortableZip;

        var hasInstaller = windowsAssets.Any(a =>
            InstallerMarkers.Any(m => a.Contains(m, StringComparison.OrdinalIgnoreCase)));
        var hasPlainExe = windowsAssets.Any(a =>
            a.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
            !InstallerMarkers.Any(m => a.Contains(m, StringComparison.OrdinalIgnoreCase)));

        if (hasPlainExe) return UpdateKind.PortableExe;
        if (hasInstaller) return UpdateKind.Installer;

        return null;
    }

    /// <summary>
    /// Suggest the executable file name from release assets (for PortableZip/PortableExe).
    /// Looks for the most likely main exe, stripping version/arch suffixes.
    /// </summary>
    public static string? SuggestExecutableName(IReadOnlyList<string> assetNames, UpdateKind kind)
    {
        if (kind is not (UpdateKind.PortableZip or UpdateKind.PortableExe)) return null;

        var candidates = assetNames
            .Where(a => a.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            .Where(IsWindowsAsset)
            .Where(a => !InstallerMarkers.Any(m => a.Contains(m, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(a => a.Length) // prefer longer names (more specific)
            .ToList();

        if (candidates.Count == 0) return null;

        // For portableExe: the exe name IS the executable name (after stripping version/arch)
        var asset = candidates[0];
        var fileName = Path.GetFileNameWithoutExtension(asset);
        // strip common suffixes: -x64, -win64, -windows, -1.2.3, -v1.2.3
        var cleaned = System.Text.RegularExpressions.Regex.Replace(
            fileName, @"[-_.](v?\d+[\d._-]*\d+)?[-_.]?(x64|x86|win64|win|windows|amd64|arm64|portable|msvc|latest|stable)*$",
            "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return string.IsNullOrWhiteSpace(cleaned) ? $"{fileName}.exe" : $"{cleaned}.exe";
    }

    /// <summary>
    /// True when at least one asset looks like a Windows binary/archive.
    /// </summary>
    public static bool IsWindowsAsset(string assetName)
    {
        var lower = assetName.ToLowerInvariant();
        if (NonWindowsMarkers.Any(m => lower.Contains(m))) return false;
        if (WindowsMarkers.Any(m => lower.Contains(m))) return true;
        // no platform marker — could be cross-platform (zip without markers, plain exe)
        return ArchiveExtensions.Any(lower.EndsWith) || lower.EndsWith(".exe") || lower.EndsWith(".msi");
    }
}
