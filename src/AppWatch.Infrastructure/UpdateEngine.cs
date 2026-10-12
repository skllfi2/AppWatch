using System.Security.Cryptography;
using AppWatch.Core.Abstractions;
using AppWatch.Core.Domain;
using AppWatch.Core.Domain.Models;
using AppWatch.Infrastructure.System;
using Serilog;

namespace AppWatch.Infrastructure;

public sealed class UpdateEngine : IUpdateEngine
{
    private readonly IGitHubClient _github;
    private readonly IProcessService _process;
    private readonly IAppLauncher _launcher;
    private readonly IElevationService _elevation;
    private readonly WingetService _winget;
    private readonly FileStore _files;

    public UpdateEngine(
        IGitHubClient github, IProcessService process, IAppLauncher launcher,
        IElevationService elevation, WingetService winget, FileStore files)
    {
        _github = github;
        _process = process;
        _launcher = launcher;
        _elevation = elevation;
        _winget = winget;
        _files = files;
    }

    public async Task<UpdateCheckResult> CheckAsync(TrackedApp app, CancellationToken cancellationToken = default)
    {
        try
        {
            if (app.UpdateKind == UpdateKind.WingetPackage)
                return new(app, UpdateStatus.UpToDate,
                    Detail: "winget packages are updated via scheduled upgrade-all");

            if (app.InstallDirectory is null)
                return new(app, UpdateStatus.Error, Detail: "install directory is not configured");

            var releases = await _github.GetReleasesAsync(app.RepoOwner, app.RepoName, cancellationToken);
            return UpdateDecisionEngine.Evaluate(app, releases, ArchitectureDetector.Detect());
        }
        catch (Exception ex)
        {
            Log.Error(ex, "check failed for {App}", app.Name);
            return new(app, UpdateStatus.Error, Detail: ex.Message);
        }
    }

    public async Task<UpdateHistoryEntry> ApplyAsync(
        TrackedApp app, UpdateCheckResult check,
        IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        var from = app.CurrentVersion ?? "unknown";
        try
        {
            if (check.Status != UpdateStatus.UpdateAvailable || check.NewRelease is null || check.ChosenAsset is null)
                throw new InvalidOperationException($"nothing to apply: {check.Status}");
            if (app.UpdateKind != UpdateKind.PortableZip)
                throw new NotSupportedException($"update kind '{app.UpdateKind}' is not supported yet");
            if (app.InstallDirectory is null)
                throw new InvalidOperationException("install directory is not configured");

            var installDir = app.InstallDirectory;
            var tag = check.NewRelease.TagName;

            var processName = app.ProcessName
                ?? (app.ExecutableName is null ? null : Path.GetFileNameWithoutExtension(app.ExecutableName));
            if (processName is not null && _process.IsRunning(processName))
            {
                Log.Information("stopping {Process} before update of {App}", processName, app.Name);
                _process.Kill(processName);
            }

            var zipPath = await DownloadAndVerifyAsync(check.ChosenAsset, app.RepoName, tag, progress, cancellationToken);

            var contentRoot = _files.ExtractToStaging(zipPath, Sanitize(tag));
            _files.ReplaceWithRollback(contentRoot, installDir);

            var newExe = app.ExecutableName is null ? null : Path.Combine(installDir, app.ExecutableName);
            if (newExe is not null && !File.Exists(newExe))
            {
                _files.RestoreBackup(installDir);
                throw new InvalidOperationException(
                    $"executable '{app.ExecutableName}' not found in new release; old version restored");
            }

            _files.DiscardBackup(installDir);
            try { File.Delete(zipPath); } catch (IOException) { }

            if (app.LaunchAfterUpdate && newExe is not null)
                _launcher.Launch(newExe);

            Log.Information("updated {App}: {From} -> {To}", app.Name, from, tag);
            app.CurrentVersion = tag;
            return new UpdateHistoryEntry(app.Name, from, tag, DateTimeOffset.UtcNow, true, null);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "update failed for {App}", app.Name);
            return new UpdateHistoryEntry(
                app.Name, from, check.NewRelease?.TagName ?? "?", DateTimeOffset.UtcNow, false, ex.Message);
        }
    }

    public async Task<UpdateHistoryEntry> InstallAsync(TrackedApp app, CancellationToken cancellationToken = default)
    {
        var from = app.CurrentVersion ?? "not installed";
        try
        {
            switch (app.UpdateKind)
            {
                case UpdateKind.WingetPackage:
                {
                    if (app.WingetPackageId is null)
                        throw new InvalidOperationException("winget package id is not configured");
                    await _winget.InstallAsync(app.WingetPackageId, cancellationToken);
                    break;
                }
                case UpdateKind.Installer:
                {
                    if (app.InstallDirectory is null)
                        throw new InvalidOperationException("install directory is not configured");
                    var check = await CheckAsync(app, cancellationToken);
                    if (check.ChosenAsset is null)
                        throw new InvalidOperationException(check.Detail ?? "no matching asset");
                    var installerPath = await DownloadAndVerifyAsync(
                        check.ChosenAsset, app.RepoName, check.NewRelease!.TagName,
                        progress: null, cancellationToken: cancellationToken);
                    if (!_elevation.RunElevated(installerPath, ""))
                        throw new InvalidOperationException("installer launch declined or failed (UAC)");
                    break;
                }
                case UpdateKind.PortableZip:
                {
                    if (app.InstallDirectory is null)
                        throw new InvalidOperationException("install directory is not configured");
                    var check = await CheckAsync(app, cancellationToken);
                    if (check.ChosenAsset is null || check.NewRelease is null)
                        throw new InvalidOperationException(check.Detail ?? "no matching asset");
                    var zipPath = await DownloadAndVerifyAsync(
                        check.ChosenAsset, app.RepoName, check.NewRelease.TagName,
                        progress: null, cancellationToken: cancellationToken);
                    var contentRoot = _files.ExtractToStaging(zipPath, Sanitize(check.NewRelease.TagName));
                    if (Directory.Exists(app.InstallDirectory)) Directory.Delete(app.InstallDirectory, true);
                    Directory.Move(contentRoot, app.InstallDirectory);
                    try { File.Delete(zipPath); } catch (IOException) { }
                    app.CurrentVersion = check.NewRelease.TagName;
                    if (app.LaunchAfterUpdate && app.ExecutableName is not null)
                        _launcher.Launch(Path.Combine(app.InstallDirectory, app.ExecutableName));
                    break;
                }
            }

            Log.Information("installed {App}", app.Name);
            return new UpdateHistoryEntry(
                app.Name, from, app.CurrentVersion ?? "?", DateTimeOffset.UtcNow, true, null);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "install failed for {App}", app.Name);
            return new UpdateHistoryEntry(app.Name, from, "?", DateTimeOffset.UtcNow, false, ex.Message);
        }
    }

    private async Task<string> DownloadAndVerifyAsync(
        ReleaseAsset asset, string repoName, string tag,
        IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(
            Path.GetTempPath(), $"appwatch-{repoName}-{Sanitize(tag)}{Path.GetExtension(asset.Name)}");
        await _github.DownloadFileAsync(asset.DownloadUrl, path, progress, cancellationToken);

        if (asset.DigestSha256 is { } digest)
            await _files.VerifyDigestAsync(path, digest);
        else
            Log.Warning("no digest published for {Asset}; skipping integrity verification", asset.Name);

        return path;
    }

    private static string Sanitize(string tag) =>
        string.Concat(tag.Where(char.IsLetterOrDigit).Take(40));
}
