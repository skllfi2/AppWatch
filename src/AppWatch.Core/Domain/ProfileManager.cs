using System.Text.Json;
using System.Text.Json.Serialization;
using AppWatch.Core.Domain.Models;

namespace AppWatch.Core.Domain;

/// <summary>
/// Data Transfer Object representing a portable tracked application in an exported manifest.
/// Uses macro-contracted paths (%APPS%, %USERPROFILE%) instead of machine-specific absolute paths.
/// </summary>
public sealed class TrackedAppDto
{
    public required string Name { get; set; }
    public required string RepoOwner { get; set; }
    public required string RepoName { get; set; }
    public string? Description { get; set; }
    public string? InstallDirectory { get; set; }
    public string? ExecutableName { get; set; }
    public UpdateKind UpdateKind { get; set; } = UpdateKind.PortableZip;
    public string? WingetPackageId { get; set; }
    public string? ProcessName { get; set; }
    public bool LaunchAfterUpdate { get; set; }
    public bool AutoApplyConsent { get; set; }
    public bool AllowUnsignedInstaller { get; set; }
    public bool IgnorePreReleases { get; set; } = true;
    public string? PinnedVersion { get; set; }
    public string? SkippedVersion { get; set; }
    public string? CurrentVersion { get; set; }
    public bool Enabled { get; set; } = true;
    public bool AutoDetectVersion { get; set; } = true;
    public string? RegistryKeyName { get; set; }
    public string? VersionExeName { get; set; }
    public string? VersionFilePath { get; set; }
    public string? VersionFileRegex { get; set; }
    public string? AvatarUrl { get; set; }
    public UpdateStrategy Strategy { get; set; } = UpdateStrategy.Auto;
    public AppCategory Category { get; set; } = AppCategory.Uncategorized;
    public AppSourceOrigin SourceOrigin { get; set; } = AppSourceOrigin.GitHubRelease;
    public List<string> Tags { get; set; } = [];
    public bool IsInPlaceInstall { get; set; }
}

/// <summary>
/// Portable manifest structure for migration across machines or dotfiles backup.
/// Tokens and system secrets are omitted by default for safety.
/// </summary>
public sealed class ProfileManifest
{
    public int ManifestVersion { get; set; } = 1;
    public DateTimeOffset ExportedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? GitHubToken { get; set; }
    public string Language { get; set; } = "ru";
    public AppTheme Theme { get; set; } = AppTheme.System;
    public bool AutoCheckEnabled { get; set; } = true;
    public bool AutoApplyUpdates { get; set; } = false;
    public int CheckIntervalHours { get; set; } = 6;
    public List<string> DownloadMirrors { get; set; } = ["https://ghfast.top/", "https://ghproxy.net/"];
    public List<TrackedAppDto> TrackedApps { get; set; } = [];
}

public sealed record ManifestImportReport(int AddedCount, int SkippedDuplicatesCount);

/// <summary>
/// Manages export and import of application profiles with secret redaction and path macro expansion.
/// </summary>
public static class ProfileManager
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    /// <summary>
    /// Exports configuration as a portable, sanitized manifest JSON.
    /// Excludes API tokens unless explicitly requested, and contracts paths using %APPS% / %USERPROFILE%.
    /// </summary>
    public static string ExportManifestJson(AppConfig config, bool includeSecrets = false, string? customAppsRoot = null)
    {
        ArgumentNullException.ThrowIfNull(config);

        var manifest = new ProfileManifest
        {
            ManifestVersion = 1,
            ExportedAtUtc = DateTimeOffset.UtcNow,
            GitHubToken = includeSecrets ? config.GitHubToken : null,
            Language = config.Language,
            Theme = config.Theme,
            AutoCheckEnabled = config.AutoCheckEnabled,
            AutoApplyUpdates = config.AutoApplyUpdates,
            CheckIntervalHours = config.CheckIntervalHours,
            DownloadMirrors = [.. config.DownloadMirrors],
            TrackedApps = config.TrackedApps.Select(app => new TrackedAppDto
            {
                Name = app.Name,
                RepoOwner = app.RepoOwner,
                RepoName = app.RepoName,
                Description = app.Description,
                InstallDirectory = PathMacroResolver.Contract(app.InstallDirectory, customAppsRoot),
                ExecutableName = app.ExecutableName,
                UpdateKind = app.UpdateKind,
                WingetPackageId = app.WingetPackageId,
                ProcessName = app.ProcessName,
                LaunchAfterUpdate = app.LaunchAfterUpdate,
                AutoApplyConsent = app.AutoApplyConsent,
                AllowUnsignedInstaller = app.AllowUnsignedInstaller,
                IgnorePreReleases = app.IgnorePreReleases,
                PinnedVersion = app.PinnedVersion,
                SkippedVersion = app.SkippedVersion,
                CurrentVersion = app.CurrentVersion,
                Enabled = app.Enabled,
                AutoDetectVersion = app.AutoDetectVersion,
                RegistryKeyName = app.RegistryKeyName,
                VersionExeName = app.VersionExeName,
                VersionFilePath = PathMacroResolver.Contract(app.VersionFilePath, customAppsRoot),
                VersionFileRegex = app.VersionFileRegex,
                AvatarUrl = app.AvatarUrl,
                Strategy = app.Strategy,
                Category = app.Category,
                SourceOrigin = app.SourceOrigin,
                Tags = [.. app.Tags],
                IsInPlaceInstall = app.IsInPlaceInstall
            }).ToList()
        };

        return JsonSerializer.Serialize(manifest, JsonOptions);
    }

    /// <summary>
    /// Parses a manifest JSON string into a ProfileManifest instance.
    /// </summary>
    public static ProfileManifest? ParseManifestJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        return JsonSerializer.Deserialize<ProfileManifest>(json, JsonOptions);
    }

    /// <summary>
    /// Applies a manifest into the target AppConfig, expanding path macros and deduplicating apps.
    /// </summary>
    public static ManifestImportReport ApplyManifest(ProfileManifest manifest, AppConfig targetConfig, string? customAppsRoot = null)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(targetConfig);

        var added = 0;
        var skipped = 0;

        foreach (var dto in manifest.TrackedApps)
        {
            if (string.IsNullOrWhiteSpace(dto.RepoName) || string.IsNullOrWhiteSpace(dto.RepoOwner))
            {
                skipped++;
                continue;
            }

            // Deduplicate by Owner and Repo
            if (targetConfig.TrackedApps.Any(a =>
                string.Equals(a.RepoOwner, dto.RepoOwner, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(a.RepoName, dto.RepoName, StringComparison.OrdinalIgnoreCase)))
            {
                skipped++;
                continue;
            }

            var expandedInstallDir = PathMacroResolver.Expand(dto.InstallDirectory, customAppsRoot);
            var expandedVersionFilePath = PathMacroResolver.Expand(dto.VersionFilePath, customAppsRoot);

            var app = new TrackedApp
            {
                Name = dto.Name,
                RepoOwner = dto.RepoOwner,
                RepoName = dto.RepoName,
                Description = dto.Description,
                InstallDirectory = expandedInstallDir,
                ExecutableName = dto.ExecutableName,
                UpdateKind = dto.UpdateKind,
                WingetPackageId = dto.WingetPackageId,
                ProcessName = dto.ProcessName,
                LaunchAfterUpdate = dto.LaunchAfterUpdate,
                AutoApplyConsent = dto.AutoApplyConsent,
                AllowUnsignedInstaller = dto.AllowUnsignedInstaller,
                IgnorePreReleases = dto.IgnorePreReleases,
                PinnedVersion = dto.PinnedVersion,
                SkippedVersion = dto.SkippedVersion,
                CurrentVersion = dto.CurrentVersion,
                Enabled = dto.Enabled,
                AutoDetectVersion = dto.AutoDetectVersion,
                RegistryKeyName = dto.RegistryKeyName,
                VersionExeName = dto.VersionExeName,
                VersionFilePath = expandedVersionFilePath,
                VersionFileRegex = dto.VersionFileRegex,
                AvatarUrl = dto.AvatarUrl,
                Strategy = dto.Strategy,
                Category = dto.Category,
                SourceOrigin = dto.SourceOrigin,
                Tags = [.. dto.Tags],
                IsInPlaceInstall = dto.IsInPlaceInstall
            };

            targetConfig.TrackedApps.Add(app);
            added++;
        }

        return new ManifestImportReport(added, skipped);
    }
}
