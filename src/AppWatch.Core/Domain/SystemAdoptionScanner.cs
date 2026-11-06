using System.Diagnostics;
using System.Runtime.Versioning;
using AppWatch.Core.Domain.Models;

namespace AppWatch.Core.Domain;

public enum AdoptionSource
{
    PathEnvironment,
    CargoBin,
    NpmGlobal,
    PipxBin,
    GoBin,
    DotnetTools,
    CustomDirectory,
}

public sealed record DiscoveredBinary(
    string FilePath,
    string FileName,
    string? ProductName,
    string? FileVersion,
    AdoptionSource Source,
    string? DetectedRepoOwner = null,
    string? DetectedRepoName = null
);

/// <summary>
/// Scans the operating system landscape (PATH, .cargo/bin, npm, pipx, local tools directories)
/// to discover previously installed CLI and portable tools and match them against known repositories.
/// </summary>
public static class SystemAdoptionScanner
{
    /// <summary>
    /// Matches a discovered binary candidate against the catalog of known repositories.
    /// Priority:
    /// 1. Exact ExecutableName match
    /// 2. Exact ProductName match
    /// 3. Name or RepoName match
    /// </summary>
    public static CatalogEntry? TryMatch(DiscoveredBinary candidate, IReadOnlyList<CatalogEntry> catalog)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(catalog);

        // 1. Direct executable name match
        var exeMatch = catalog.FirstOrDefault(c =>
            !string.IsNullOrEmpty(c.ExecutableName) &&
            string.Equals(c.ExecutableName, candidate.FileName, StringComparison.OrdinalIgnoreCase));
        if (exeMatch is not null) return exeMatch;

        // 2. File name without extension match with RepoName or ExecutableName
        var nameWithoutExt = Path.GetFileNameWithoutExtension(candidate.FileName);
        var baseMatch = catalog.FirstOrDefault(c =>
            string.Equals(c.RepoName, nameWithoutExt, StringComparison.OrdinalIgnoreCase) ||
            (!string.IsNullOrEmpty(c.ExecutableName) &&
             string.Equals(Path.GetFileNameWithoutExtension(c.ExecutableName), nameWithoutExt, StringComparison.OrdinalIgnoreCase)));
        if (baseMatch is not null) return baseMatch;

        // 3. ProductName from PE header match
        if (!string.IsNullOrWhiteSpace(candidate.ProductName))
        {
            var productMatch = catalog.FirstOrDefault(c =>
                string.Equals(c.Name, candidate.ProductName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(c.RepoName, candidate.ProductName, StringComparison.OrdinalIgnoreCase));
            if (productMatch is not null) return productMatch;
        }

        return null;
    }

    /// <summary>
    /// Builds a TrackedApp entity for In-Place management (without moving original user files).
    /// </summary>
    public static TrackedApp CreateInPlaceTrackedApp(DiscoveredBinary binary, CatalogEntry catalog)
    {
        ArgumentNullException.ThrowIfNull(binary);
        ArgumentNullException.ThrowIfNull(catalog);

        var installDir = Path.GetDirectoryName(binary.FilePath) ?? "";

        var category = AppCategory.Uncategorized;
        if (!string.IsNullOrEmpty(catalog.Category) &&
            Enum.TryParse<AppCategory>(catalog.Category, ignoreCase: true, out var parsedCat))
        {
            category = parsedCat;
        }

        return new TrackedApp
        {
            Name = catalog.Name,
            RepoOwner = catalog.RepoOwner,
            RepoName = catalog.RepoName,
            Description = catalog.Description,
            InstallDirectory = installDir,
            ExecutableName = Path.GetFileName(binary.FilePath),
            CurrentVersion = binary.FileVersion,
            UpdateKind = catalog.UpdateKind,
            Category = category,
            SourceOrigin = AppSourceOrigin.LocalInPlace,
            IsInPlaceInstall = true,
            Enabled = true
        };
    }

    /// <summary>
    /// Extracts PE FileVersionInfo metadata from a Windows executable safely.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static DiscoveredBinary InspectExecutable(string filePath, AdoptionSource source)
    {
        var fileName = Path.GetFileName(filePath);
        string? productName = null;
        string? fileVersion = null;

        try
        {
            if (File.Exists(filePath))
            {
                var fvi = FileVersionInfo.GetVersionInfo(filePath);
                productName = fvi.ProductName;
                fileVersion = fvi.ProductVersion ?? fvi.FileVersion;
            }
        }
        catch
        {
            // Ignore corrupted or unreadable binaries safely
        }

        return new DiscoveredBinary(filePath, fileName, productName, fileVersion, source);
    }

    /// <summary>
    /// Scans a directory for executable files (.exe).
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static List<DiscoveredBinary> ScanDirectory(string directory, AdoptionSource source, bool topDirectoryOnly = true)
    {
        var result = new List<DiscoveredBinary>();
        if (!Directory.Exists(directory)) return result;

        try
        {
            var option = topDirectoryOnly ? SearchOption.TopDirectoryOnly : SearchOption.AllDirectories;
            var exes = Directory.EnumerateFiles(directory, "*.exe", option);
            foreach (var exe in exes)
            {
                result.Add(InspectExecutable(exe, source));
            }
        }
        catch
        {
            // Tolerate permission denied on protected directories
        }

        return result;
    }
}
