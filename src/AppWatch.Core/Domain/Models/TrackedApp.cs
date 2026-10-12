namespace AppWatch.Core.Domain.Models;

public sealed class TrackedApp
{
    public required string Name { get; set; }
    public required string RepoOwner { get; set; }
    public required string RepoName { get; set; }
    public string? InstallDirectory { get; set; }
    public string? ExecutableName { get; set; }
    public UpdateKind UpdateKind { get; set; } = UpdateKind.PortableZip;
    public string? WingetPackageId { get; set; }
    public string? ProcessName { get; set; }
    public bool LaunchAfterUpdate { get; set; }
    public IReadOnlyList<string>? AssetExtensionPriority { get; set; }
    public bool IgnorePreReleases { get; set; } = true;
    public string? PinnedVersion { get; set; }
    public string? CurrentVersion { get; set; }
    public bool Enabled { get; set; } = true;
}
