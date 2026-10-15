using AppWatch.Core.Domain.Models;

namespace AppWatch.Core.Abstractions;

public interface IGitHubClient
{
    Task<IReadOnlyList<ReleaseInfo>> GetReleasesAsync(
        string owner, string repo, CancellationToken cancellationToken = default, bool skipCache = false);

    Task DownloadFileAsync(
        string url, string destinationPath,
        IProgress<double>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>True when GitHub API rate limit is exhausted; wait for RateLimitResetUtc.</summary>
    bool IsRateLimited { get; }

    /// <summary>UTC timestamp when the rate limit window resets; null when unknown.</summary>
    DateTimeOffset? RateLimitResetUtc { get; }

    /// <summary>Remaining requests in the current window; null when unknown.</summary>
    int? RemainingRequests { get; }
}

public interface IUpdateEngine
{
    Task<UpdateCheckResult> CheckAsync(TrackedApp app, CancellationToken cancellationToken = default);
    Task<UpdateHistoryEntry> ApplyAsync(TrackedApp app, UpdateCheckResult check,
        IProgress<double>? progress = null, CancellationToken cancellationToken = default);
    Task<UpdateHistoryEntry> InstallAsync(TrackedApp app, CancellationToken cancellationToken = default);
}

public interface IProcessService
{
    bool IsRunning(string processName);
    /// <summary>
    /// Best-effort kill by name. Returns false when at least one process survived
    /// (e.g. an elevated process cannot be opened from a non-elevated AppWatch).
    /// </summary>
    bool Kill(string processName);
}

public interface IAppLauncher
{
    void Launch(string executablePath);
}

public interface IConfigStore
{
    AppConfig Load();
    void Save(AppConfig config);
}

public interface IElevationService
{
    bool IsElevated { get; }
    bool RunElevated(string executablePath, string arguments);
}
