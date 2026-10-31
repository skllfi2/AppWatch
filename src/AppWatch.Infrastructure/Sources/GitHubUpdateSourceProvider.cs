using AppWatch.Core.Abstractions;
using AppWatch.Core.Domain;
using AppWatch.Core.Domain.Models;
using AppWatch.Core.Domain.VersionDetection;
using AppWatch.Infrastructure.System;
using Serilog;

namespace AppWatch.Infrastructure.Sources;

public sealed class GitHubUpdateSourceProvider : IUpdateSourceProvider
{
    private readonly IGitHubClient _github;
    private readonly SystemVersionResolver? _systemVersion;

    public string SourceId => "github";
    public string DisplayName => "GitHub Releases";

    public GitHubUpdateSourceProvider(IGitHubClient github, SystemVersionResolver? systemVersion = null)
    {
        _github = github;
        _systemVersion = systemVersion;
    }

    public bool CanHandle(TrackedApp app)
    {
        return !string.IsNullOrWhiteSpace(app.RepoOwner) &&
               !string.IsNullOrWhiteSpace(app.RepoName) &&
               app.UpdateKind != UpdateKind.External &&
               app.UpdateKind != UpdateKind.WingetPackage;
    }

    public async Task<UpdateCheckResult> CheckUpdateAsync(TrackedApp app, CancellationToken cancellationToken = default)
    {
        if (app.InstallDirectory is null)
        {
            _systemVersion?.AutoDetectInstalledApp(app);
        }

        if (app.InstallDirectory is null && app.UpdateKind is UpdateKind.PortableZip or UpdateKind.PortableExe)
            return new(app, UpdateStatus.Error, Detail: "install directory is not configured");

        // Auto-detect installed version before comparing (fixes stale CurrentVersion)
        if (app.AutoDetectVersion)
        {
            var detected = await VersionDetector.DetectAsync(app, cancellationToken).ConfigureAwait(false);
            if (detected is not null && detected.Version != app.CurrentVersion)
            {
                Log.Information("version auto-detected for {App}: {Version} ({Source}), stored: {Stored}",
                    app.Name, detected.Version, detected.Source, app.CurrentVersion ?? "none");
                app.CurrentVersion = detected.Version;
            }
        }

        // Rate-limit aware: skip check entirely when the window is exhausted
        if (_github.IsRateLimited)
            return new(app, UpdateStatus.Error, Detail: $"rate limited until {_github.RateLimitResetUtc:HH:mm:ss} UTC");

        var releases = await _github.GetReleasesAsync(app.RepoOwner, app.RepoName, cancellationToken).ConfigureAwait(false);
        return UpdateDecisionEngine.Evaluate(app, releases, ArchitectureDetector.Detect());
    }
}
