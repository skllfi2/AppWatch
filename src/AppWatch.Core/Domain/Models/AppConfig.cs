namespace AppWatch.Core.Domain.Models;

public sealed class AppConfig
{
    public int SchemaVersion { get; set; } = 2;
    public string? GitHubToken { get; set; }
    public string Language { get; set; } = "ru";
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
}
