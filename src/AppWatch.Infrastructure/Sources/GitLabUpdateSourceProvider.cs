using AppWatch.Core.Abstractions;
using AppWatch.Core.Domain;
using AppWatch.Core.Domain.Models;
using AppWatch.Core.Domain.VersionDetection;
using AppWatch.Infrastructure.Git;
using AppWatch.Infrastructure.System;

namespace AppWatch.Infrastructure.Sources;

/// <summary>
/// Source provider for applications hosted on GitLab (GitLab REST API v4).
/// </summary>
public sealed class GitLabUpdateSourceProvider : IUpdateSourceProvider
{
    private readonly GitLabApiClient _client;
    private readonly SystemVersionResolver? _systemVersion;

    public string SourceId => "gitlab";
    public string DisplayName => "GitLab Releases";

    public GitLabUpdateSourceProvider(GitLabApiClient client, SystemVersionResolver? systemVersion = null)
    {
        _client = client;
        _systemVersion = systemVersion;
    }

    public bool CanHandle(TrackedApp app)
    {
        return app.SourceOrigin == AppSourceOrigin.GitLab &&
               !string.IsNullOrWhiteSpace(app.RepoOwner) &&
               !string.IsNullOrWhiteSpace(app.RepoName);
    }

    public async Task<UpdateCheckResult> CheckUpdateAsync(TrackedApp app, CancellationToken cancellationToken = default)
    {
        if (app.InstallDirectory is null)
        {
            _systemVersion?.AutoDetectInstalledApp(app);
        }

        if (app.InstallDirectory is null && app.UpdateKind is UpdateKind.PortableZip or UpdateKind.PortableExe)
            return new(app, UpdateStatus.Error, Detail: "install directory is not configured");

        if (app.AutoDetectVersion)
        {
            var detected = await VersionDetector.DetectAsync(app, cancellationToken).ConfigureAwait(false);
            if (detected is not null && detected.Version != app.CurrentVersion)
            {
                app.CurrentVersion = detected.Version;
            }
        }

        var releases = await _client.GetReleasesAsync(app.RepoOwner, app.RepoName, cancellationToken).ConfigureAwait(false);
        return UpdateDecisionEngine.Evaluate(app, releases, ArchitectureDetector.Detect());
    }
}
