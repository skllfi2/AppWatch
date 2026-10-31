namespace AppWatch.Core.Domain.Models;

public enum AppTheme
{
    System,
    Dark,
    Light,
}

public sealed class AppConfig
{
    public int SchemaVersion { get; set; } = 2;
    public string? GitHubToken { get; set; }
    public string Language { get; set; } = "ru";
    public AppTheme Theme { get; set; } = AppTheme.System;
    public bool AutoCheckEnabled { get; set; } = true;
    public bool AutoApplyUpdates { get; set; } = false;
    public int CheckIntervalHours { get; set; } = 6;
    public int HealthCheckSeconds { get; set; } = UpdateHealthCheck.DefaultWindowSeconds;
    public UpdateStrategy DefaultUpdateStrategy { get; set; } = UpdateStrategy.Auto;
    public string? InstallRoot { get; set; }
    public List<TrackedApp> TrackedApps { get; set; } = [];

    // D20: background monitor next-run timestamps (UTC) for UI display
    public DateTimeOffset? BackgroundCheckNextRunUtc { get; set; }
    public DateTimeOffset? CatalogCheckNextRunUtc { get; set; }

    /// <summary>UTC time of the last completed check cycle (manual or background) — footer "Последняя проверка".</summary>
    public DateTimeOffset? LastCheckUtc { get; set; }

    /// <summary>
    /// B8: ghproxy-style mirror prefixes tried (in order) when a direct github.com
    /// release download fails. Mirror serves the full GitHub URL as a path suffix.
    /// Verified working 2026-10-14 (Range/206 supported by both).
    /// </summary>
    public List<string> DownloadMirrors { get; set; } = ["https://ghfast.top/", "https://ghproxy.net/"];

    /// <summary>B10: how many old versions to keep per app in the version cache (newest first).</summary>
    public int VersionCacheCount { get; set; } = 3;

    /// <summary>Show the onboarding dialog on first launch.</summary>
    public bool ShowOnboarding { get; set; } = true;

    /// <summary>Show a Windows app notification when background checks find updates.</summary>
    public bool ToastNotificationsEnabled { get; set; } = true;

    /// <summary>Automatically refresh application icons on update checks and after installs.</summary>
    public bool AutoRefreshIcons { get; set; } = true;
}
