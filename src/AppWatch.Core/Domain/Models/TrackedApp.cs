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

    /// <summary>
    /// Trust model (этап 6 плана): explicit per-app consent for UNATTENDED updates.
    /// The global AutoApplyUpdates flag alone is NOT enough — the auto-apply only
    /// fires when the user opted in for this specific app. Default OFF.
    /// </summary>
    public bool AutoApplyConsent { get; set; }

    /// <summary>B7: explicit per-app consent to run unsigned installers (most small GitHub tools ship unsigned).</summary>
    public bool AllowUnsignedInstaller { get; set; }

    /// <summary>B9: outcome of the last update check (persisted — drives background check priority).</summary>
    public UpdateStatus? LastCheckStatus { get; set; }

    /// <summary>B9: when the app was last checked (UTC).</summary>
    public DateTimeOffset? LastCheckUtc { get; set; }

    /// <summary>P6: consecutive error count for backoff. Reset to 0 on any non-Error status.</summary>
    public int ConsecutiveErrorCount { get; set; }
    public IReadOnlyList<string>? AssetExtensionPriority { get; set; }
    public bool IgnorePreReleases { get; set; } = true;
    public string? PinnedVersion { get; set; }

    /// <summary>A1: user explicitly skipped this version — engine treats it as UpToDate
    /// until a NEWER release appears or the user resets the rule.</summary>
    public string? SkippedVersion { get; set; }

    public string? CurrentVersion { get; set; }
    public bool Enabled { get; set; } = true;

    // New: Version detection settings
    public bool AutoDetectVersion { get; set; } = true;
    public string? RegistryKeyName { get; set; }
    public string? VersionExeName { get; set; }

    /// <summary>P3: path to a data file containing the version string (env vars expanded).
    /// Battle.net: %ProgramFiles(x86)%\Battle.net\.product.db — the exe has no FVI,
    /// no registry entry; the version lives in this small protobuf file.</summary>
    public string? VersionFilePath { get; set; }

    /// <summary>P3: regex with one capture group extracting the version from VersionFilePath content.
    /// File is read as Latin-1 so ASCII strings inside binary files survive.</summary>
    public string? VersionFileRegex { get; set; }

        // New: GitHub repo avatar URL for card icon
        public string? AvatarUrl { get; set; }

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
