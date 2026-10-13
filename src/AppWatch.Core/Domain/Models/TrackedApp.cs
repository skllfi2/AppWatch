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

    // New: Version detection settings
    public bool AutoDetectVersion { get; set; } = true;
    public string? RegistryKeyName { get; set; }
    public string? VersionExeName { get; set; }

    // New: Update strategy
    public UpdateStrategy Strategy { get; set; } = UpdateStrategy.Auto;
}

public enum UpdateStrategy
{
    /// <summary>Let the engine decide based on asset type and app capabilities</summary>
    Auto = 0,
    
    /// <summary>Use built-in app updater (if app has self-update)</summary>
    BuiltInUpdater,
    
    /// <summary>Silent installer (msi/exe with /quiet)</summary>
    SilentInstaller,
    
    /// <summary>Portable archive replace (zip/7z)</summary>
    PortableReplace,
    
    /// <summary>Use winget if package ID known</summary>
    Winget,
    
    /// <summary>External launcher - just download and run</summary>
    ExternalLauncher,
    
    /// <summary>User decides manually</summary>
    Manual
}
