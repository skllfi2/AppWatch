using AppWatch.Core.Domain.Models;

namespace AppWatch.Core.Abstractions;

public interface IGitHubClient
{
    Task<IReadOnlyList<ReleaseInfo>> GetReleasesAsync(
        string owner, string repo, CancellationToken cancellationToken = default, bool skipCache = false);

    Task DownloadFileAsync(
        string url, string destinationPath,
        IProgress<double>? progress = null, CancellationToken cancellationToken = default);

    Task<GitHubRepoInfo?> GetRepoInfoAsync(string owner, string repo, CancellationToken cancellationToken = default);

    /// <summary>True when GitHub API rate limit is exhausted; wait for RateLimitResetUtc.</summary>
    bool IsRateLimited { get; }

    /// <summary>UTC timestamp when the rate limit window resets; null when unknown.</summary>
    DateTimeOffset? RateLimitResetUtc { get; }

    /// <summary>Remaining requests in the current window; null when unknown.</summary>
    int? RemainingRequests { get; }
}

public sealed class GitHubRepoInfo
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required string HtmlUrl { get; init; }
    public string? Homepage { get; init; }
    public required string AvatarUrl { get; init; }
    public required string OwnerLogin { get; init; }
    public required string OwnerAvatarUrl { get; init; }
    public string? IconUrl { get; init; }
}

public interface IUpdateEngine
{
    Task<UpdateCheckResult> CheckAsync(TrackedApp app, CancellationToken cancellationToken = default);
    Task<UpdateHistoryEntry> ApplyAsync(TrackedApp app, UpdateCheckResult check,
        IProgress<double>? progress = null, CancellationToken cancellationToken = default);
    Task<UpdateHistoryEntry> InstallAsync(TrackedApp app, CancellationToken cancellationToken = default);

    /// <summary>Removes the app: winget uninstall for packages, delete install dir for
    /// AppWatch-managed portables (external dirs are only untracked, never deleted).</summary>
    Task<UpdateHistoryEntry> UninstallAsync(TrackedApp app, CancellationToken cancellationToken = default);
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

public interface IShortcutService
{
    bool CreateStartMenuShortcut(string appName, string executablePath, string? description = null);
    bool CreateDesktopShortcut(string appName, string executablePath, string? description = null);
    string? ResolveShortcutTarget(string shortcutPath);
}

public interface IUpdateSourceProvider
{
    string SourceId { get; }
    string DisplayName { get; }
    bool CanHandle(TrackedApp app);
    Task<UpdateCheckResult> CheckUpdateAsync(TrackedApp app, CancellationToken cancellationToken = default);
}

public interface IUpdateSourceRegistry
{
    IReadOnlyList<IUpdateSourceProvider> Providers { get; }
    void Register(IUpdateSourceProvider provider);
    IUpdateSourceProvider? Resolve(TrackedApp app);
}
