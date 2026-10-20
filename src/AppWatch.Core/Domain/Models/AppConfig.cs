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

    /// <summary>
    /// B8: ghproxy-style mirror prefixes tried (in order) when a direct github.com
    /// release download fails. Mirror serves the full GitHub URL as a path suffix.
    /// Verified working 2026-10-14 (Range/206 supported by both).
    /// </summary>
    public List<string> DownloadMirrors { get; set; } = ["https://ghfast.top/", "https://ghproxy.net/"];

    /// <summary>B10: how many old versions to keep per app in the version cache (newest first).</summary>
    public int VersionCacheCount { get; set; } = 3;

    // === P-Sem: semantic catalog search (off by default; readiness for large catalogs) ===

    /// <summary>User opt-in for semantic (embedding-based) catalog search.</summary>
    public bool SemanticSearchEnabled { get; set; } = false;

    /// <summary>Model download URL override (ONNX, int8-quantized sentence-transformer). Null = built-in default.</summary>
    public string? EmbeddingModelUrl { get; set; }

    /// <summary>HF tokenizer.json URL override. Null = built-in default.</summary>
    public string? EmbeddingTokenizerUrl { get; set; }

    /// <summary>Show the onboarding dialog on first launch.</summary>
    public bool ShowOnboarding { get; set; } = true;

    /// <summary>Show a Windows app notification when background checks find updates.</summary>
    public bool ToastNotificationsEnabled { get; set; } = true;
}
