namespace AppWatch.Core.Domain.Models;

public sealed class CatalogEntry
{
    public required string Name { get; set; }
    public required string RepoOwner { get; set; }
    public required string RepoName { get; set; }
    public UpdateKind UpdateKind { get; set; } = UpdateKind.PortableZip;
    public string? ExecutableName { get; set; }
    public string? ProcessName { get; set; }
    public string? WingetPackageId { get; set; }
    public IReadOnlyList<string>? AssetExtensionPriority { get; set; }
    public bool IgnorePreReleases { get; set; } = true;
    public string? Description { get; set; }
    public string? Category { get; set; }

    public TrackedApp ToTrackedApp(string installDirectory, string? currentVersion = null) => new()
    {
        Name = Name,
        RepoOwner = RepoOwner,
        RepoName = RepoName,
        InstallDirectory = installDirectory,
        ExecutableName = ExecutableName,
        ProcessName = ProcessName,
        UpdateKind = UpdateKind,
        WingetPackageId = WingetPackageId,
        AssetExtensionPriority = AssetExtensionPriority,
        IgnorePreReleases = IgnorePreReleases,
        CurrentVersion = currentVersion,
    };
}
