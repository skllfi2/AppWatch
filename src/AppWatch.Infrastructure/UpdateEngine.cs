using System.Diagnostics;
using System.Security.Cryptography;
using AppWatch.Core.Abstractions;
using AppWatch.Core.Domain;
using AppWatch.Core.Domain.Models;
using AppWatch.Core.Domain.VersionDetection;
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
    private readonly AppConfig _config;
    private readonly InstallerTrustService _trust;
    private readonly VersionStore _versions;
    private readonly UpdateGate _gate;
    private readonly UpdateJournalStore _journal;

    public UpdateEngine(
        IGitHubClient github, IProcessService process, IAppLauncher launcher,
        IElevationService elevation, WingetService winget, FileStore files, AppConfig config,
        InstallerTrustService trust, VersionStore versions, UpdateGate gate, UpdateJournalStore journal)
    {
        _github = github;
        _process = process;
        _launcher = launcher;
        _elevation = elevation;
        _winget = winget;
        _files = files;
        _config = config;
        _trust = trust;
        _versions = versions;
        _gate = gate;
        _journal = journal;
    }

    public async Task<UpdateCheckResult> CheckAsync(TrackedApp app, CancellationToken cancellationToken = default)
    {
        try
        {
            if (app.UpdateKind == UpdateKind.WingetPackage)
                return new(app, UpdateStatus.UpToDate,
                    Detail: "winget packages are updated via scheduled upgrade-all");

            // P3: externally managed app (Battle.net, Steam…) — detect-only, never touch GitHub
            if (app.UpdateKind == UpdateKind.External)
            {
                if (app.AutoDetectVersion)
                {
                    var detected = await VersionDetector.DetectAsync(app, cancellationToken);
                    if (detected is not null && detected.Version != app.CurrentVersion)
                    {
                        Log.Information("version auto-detected for {App}: {Version} ({Source}), stored: {Stored}",
                            app.Name, detected.Version, detected.Source, app.CurrentVersion ?? "none");
                        app.CurrentVersion = detected.Version;
                    }
                }
                return new(app, UpdateStatus.UpToDate,
                    Detail: "managed by its own updater");
            }

            if (app.InstallDirectory is null)
                return new(app, UpdateStatus.Error, Detail: "install directory is not configured");

            // Auto-detect installed version before comparing (fixes stale CurrentVersion)
            if (app.AutoDetectVersion)
            {
                var detected = await VersionDetector.DetectAsync(app, cancellationToken);
                if (detected is not null && detected.Version != app.CurrentVersion)
                {
                    Log.Information("version auto-detected for {App}: {Version} ({Source}), stored: {Stored}",
                        app.Name, detected.Version, detected.Source, app.CurrentVersion ?? "none");
                    app.CurrentVersion = detected.Version;
                }
            }

            // Rate-limit aware: skip check entirely when the window is exhausted
            if (_github.IsRateLimited)
                return new(app, UpdateStatus.Error,
                    Detail: $"rate limited until {_github.RateLimitResetUtc:HH:mm:ss} UTC");

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

                // UPD-03: refuse a concurrent operation on the same app (card button,
                // toast button and background auto-apply all land here).
                using var _gateClaim = _gate.TryAcquire(app.Name);
                if (_gateClaim is null)
                    return new UpdateHistoryEntry(
                        app.Name, from, check.NewRelease.TagName, DateTimeOffset.UtcNow, false,
                        "update already in progress");

                // Determine strategy
                var strategy = check.Strategy;
                if (strategy == UpdateStrategy.Auto)
                    strategy = UpdateDecisionEngine.DetermineStrategy(app, check.NewRelease!);

                if (app.InstallDirectory is null)
                    throw new InvalidOperationException("install directory is not configured");

                var installDir = app.InstallDirectory;
                var tag = check.NewRelease!.TagName;

                var processName = app.ProcessName
                    ?? (app.ExecutableName is null ? null : Path.GetFileNameWithoutExtension(app.ExecutableName));
                if (processName is not null && _process.IsRunning(processName))
                {
                    Log.Information("stopping {Process} before update of {App}", processName, app.Name);
                    // P0 live-fix: an elevated target (e.g. user's own winws DPI-bypass) cannot
                    // be killed from non-elevated AppWatch — abort BEFORE touching any files
                    // so the old version stays intact, with a reason the UI can show.
                    if (!_process.Kill(processName))
                        throw new InvalidOperationException(
                            $"не удалось остановить процесс «{processName}» — вероятно, он запущен от администратора. " +
                            "Остановите его вручную или запустите AppWatch от администратора.");
                }

                var installerPath = await DownloadAndVerifyAsync(check.ChosenAsset!, app.RepoName, tag, progress, cancellationToken);

                // B7: signature gate BEFORE any installer/launcher strategy runs the file.
                // PortableReplace doesn't launch the download, so it skips the gate.
                if (strategy is UpdateStrategy.SilentInstaller
                                      or UpdateStrategy.BuiltInUpdater
                                      or UpdateStrategy.ExternalLauncher)
                {
                    var trust = _trust.Verify(installerPath);
                    if (!InstallerTrustPolicy.CanRun(trust, app.AllowUnsignedInstaller))
                        throw new InvalidOperationException(
                            $"refused to run: {InstallerTrustPolicy.RefusalReason(trust)}");
                    if (trust is InstallerTrustLevel.Unsigned or InstallerTrustLevel.InvalidSignature)
                        Log.Warning("launching {Trust} installer for {App} with per-app consent", trust, app.Name);
                }

                // UPD-02: journal the in-flight mutation — a hard kill (power loss, crash)
                // from here on is detected and rolled back at next startup.
                _journal.Write(new PendingUpdate(app.Name, installDir, from, tag, DateTimeOffset.UtcNow));
                try
                {
                UpdateHistoryEntry result = strategy switch
                {
                    UpdateStrategy.Winget => await ApplyWingetAsync(app, tag, cancellationToken),
                    UpdateStrategy.BuiltInUpdater => await ApplyBuiltInUpdaterAsync(app, installerPath, cancellationToken),
                    UpdateStrategy.SilentInstaller => await ApplySilentInstallerAsync(app, installerPath, installDir, cancellationToken),
                    UpdateStrategy.PortableReplace => await ApplyPortableReplaceAsync(app, installerPath, installDir, tag, cancellationToken),
                    UpdateStrategy.ExternalLauncher => await ApplyExternalLauncherAsync(app, installerPath, cancellationToken),
                    _ => throw new NotSupportedException($"strategy '{strategy}' not supported")
                };

                if (result.Success)
                {
                    app.CurrentVersion = tag;
                }

                return result;
                }
                finally
                {
                    // every handled failure path has already restored the backup —
                    // only a hard process death leaves the journal record behind
                    _journal.Clear();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "update failed for {App}", app.Name);
                return new UpdateHistoryEntry(
                    app.Name, from, check.NewRelease?.TagName ?? "?", DateTimeOffset.UtcNow, false, ex.Message);
            }
        }

        private async Task<UpdateHistoryEntry> ApplyWingetAsync(TrackedApp app, string tag, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(app.WingetPackageId))
                return new UpdateHistoryEntry(app.Name, app.CurrentVersion ?? "?", tag, DateTimeOffset.UtcNow, false, "WingetPackageId not configured");

            try
            {
                await _winget.UpgradePackageAsync(app.WingetPackageId, app.Name, null, ct);
                return new UpdateHistoryEntry(app.Name, app.CurrentVersion ?? "?", tag, DateTimeOffset.UtcNow, true, null);
            }
            catch (Exception ex)
            {
                return new UpdateHistoryEntry(app.Name, app.CurrentVersion ?? "?", tag, DateTimeOffset.UtcNow, false, ex.Message);
            }
        }

        private async Task<UpdateHistoryEntry> ApplyBuiltInUpdaterAsync(TrackedApp app, string installerPath, CancellationToken ct)
        {
            // For apps with built-in updaters, we just launch the installer
            // The app should handle the update itself
            if (!_elevation.RunElevated(installerPath, "/quiet /norestart"))
                throw new InvalidOperationException("built-in updater launch declined or failed (UAC)");
        
            return new UpdateHistoryEntry(app.Name, app.CurrentVersion ?? "?", "built-in", DateTimeOffset.UtcNow, true, null);
        }

        private async Task<UpdateHistoryEntry> ApplySilentInstallerAsync(TrackedApp app, string installerPath, string installDir, CancellationToken ct)
        {
            // Determine silent install arguments based on extension
            var args = Path.GetExtension(installerPath).ToLowerInvariant() switch
            {
                ".msi" => $"/i \"{installerPath}\" /quiet /norestart INSTALLDIR=\"{installDir}\"",
                ".exe" => $"/quiet /norestart /dir=\"{installDir}\"",
                _ => throw new NotSupportedException($"Unsupported installer type: {Path.GetExtension(installerPath)}")
            };

            if (!_elevation.RunElevated(Path.GetFileName(installerPath), args))
                throw new InvalidOperationException("silent installer launch declined or failed (UAC)");

            return new UpdateHistoryEntry(app.Name, app.CurrentVersion ?? "?", "silent-install", DateTimeOffset.UtcNow, true, null);
        }

        private async Task<UpdateHistoryEntry> ApplyPortableReplaceAsync(TrackedApp app, string installerPath, string installDir, string tag, CancellationToken ct)
        {
            var contentRoot = _files.ExtractToStaging(installerPath, Sanitize(tag), app.ExecutableName);
            _files.ReplaceWithRollback(contentRoot, installDir);

            var newExe = app.ExecutableName is null ? null : Path.Combine(installDir, app.ExecutableName);
            if (newExe is not null && !File.Exists(newExe))
            {
                _files.RestoreBackup(installDir);
                throw new InvalidOperationException($"executable '{app.ExecutableName}' not found in new release; old version restored");
            }

            var processName = app.ProcessName
                ?? (app.ExecutableName is null ? null : Path.GetFileNameWithoutExtension(app.ExecutableName));

            // B6: health-check the new build BEFORE committing — launch it and require it to
            // stay alive for the configured window. Only then is the old version discarded.
            var health = await UpdateHealthCheck.VerifyStartupAsync(
                newExe, processName, _process, _launcher,
                windowSeconds: _config.HealthCheckSeconds, cancellationToken: ct);

            if (!health.Healthy)
            {
                _files.RestoreBackup(installDir);
                var reason = $"health check failed: {health.Reason}; previous version restored";
                Log.Warning("update of {App} rolled back: {Reason}", app.Name, health.Reason);
                // restore the user's running state when they expect the app to run
                if (app.LaunchAfterUpdate && newExe is not null && File.Exists(newExe))
                    try { _launcher.Launch(newExe); } catch { /* best effort */ }
                return new UpdateHistoryEntry(app.Name, app.CurrentVersion ?? "?", tag, DateTimeOffset.UtcNow, false, reason);
            }

            // commit: archive the old version instead of deleting it (B10 — version cache,
            // instant rollback from UI); only after a healthy start
            _versions.ArchiveBackup(installDir, app.Name, app.CurrentVersion);
            try { File.Delete(installerPath); } catch (IOException) { }

            // the health check launched the app; keep it running only when asked
            if (!app.LaunchAfterUpdate && processName is not null)
                try { _process.Kill(processName); } catch { /* best effort */ }

            return new UpdateHistoryEntry(app.Name, app.CurrentVersion ?? "?", tag, DateTimeOffset.UtcNow, true, null);
        }

        private async Task<UpdateHistoryEntry> ApplyExternalLauncherAsync(TrackedApp app, string installerPath, CancellationToken ct)
        {
            // Just launch the downloaded file, let user handle it
            Process.Start(new ProcessStartInfo
            {
                FileName = installerPath,
                UseShellExecute = true
            });
        
            return new UpdateHistoryEntry(app.Name, app.CurrentVersion ?? "?", "external-launcher", DateTimeOffset.UtcNow, true, null);
        }

    public async Task<UpdateHistoryEntry> InstallAsync(TrackedApp app, CancellationToken cancellationToken = default)
    {
        var from = app.CurrentVersion ?? "not installed";
        try
        {
            // UPD-03: same per-app lock as ApplyAsync
            using var _gateClaim = _gate.TryAcquire(app.Name);
            if (_gateClaim is null)
                return new UpdateHistoryEntry(app.Name, from, "?", DateTimeOffset.UtcNow, false,
                    "install already in progress");

            switch (app.UpdateKind)
            {
                case UpdateKind.WingetPackage:
                {
                    if (app.WingetPackageId is null)
                        throw new InvalidOperationException("winget package id is not configured");
                    var (exitCode, output) = await _winget.InstallAsync(app.WingetPackageId, cancellationToken);
                    if (exitCode != 0 && exitCode != WingetService.AlreadyInstalledExitCode)
                        throw new InvalidOperationException(
                            $"winget install failed (exit code 0x{exitCode:X8}): {WingetOutputParser.Squash(output)}");
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
                case UpdateKind.PortableZip or UpdateKind.PortableExe:
                {
                    if (app.InstallDirectory is null)
                        throw new InvalidOperationException("install directory is not configured");
                    var check = await CheckAsync(app, cancellationToken);
                    if (check.ChosenAsset is null || check.NewRelease is null)
                        throw new InvalidOperationException(check.Detail ?? "no matching asset");
                    var zipPath = await DownloadAndVerifyAsync(
                        check.ChosenAsset, app.RepoName, check.NewRelease.TagName,
                        progress: null, cancellationToken: cancellationToken);
                    var contentRoot = _files.ExtractToStaging(zipPath, Sanitize(check.NewRelease.TagName),
                        app.ExecutableName);
                    // P0 live-fix: Directory.Move does not create intermediates (%LOCALAPPDATA%\AppWatch\Apps).
                    var installParent = Path.GetDirectoryName(app.InstallDirectory);
                    if (!string.IsNullOrEmpty(installParent)) Directory.CreateDirectory(installParent);
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

    public async Task<UpdateHistoryEntry> UninstallAsync(TrackedApp app, CancellationToken cancellationToken = default)
    {
        var from = app.CurrentVersion ?? "not installed";
        try
        {
            switch (app.UpdateKind)
            {
                case UpdateKind.WingetPackage:
                {
                    if (string.IsNullOrEmpty(app.WingetPackageId))
                        throw new InvalidOperationException("WingetPackageId is not configured");
                    var (exitCode, output) = await _winget.UninstallAsync(app.WingetPackageId, cancellationToken);
                    if (exitCode != 0)
                        throw new InvalidOperationException(
                            $"winget uninstall failed (exit code 0x{exitCode:X8}): {WingetOutputParser.Squash(output)}");
                    break;
                }
                case UpdateKind.PortableZip or UpdateKind.PortableExe:
                {
                    if (app.InstallDirectory is null)
                        throw new InvalidOperationException("install directory is not configured");

                    // SAFETY: only delete directories AppWatch itself manages
                    // (%LOCALAPPDATA%\AppWatch\Apps\...). Anything outside (Program Files,
                    // user-chosen dirs) is the user's own files — untrack only.
                    var managedRoot = Path.GetFullPath(Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "AppWatch", "Apps"));
                    var target = Path.GetFullPath(app.InstallDirectory);
                    if (target.StartsWith(managedRoot, StringComparison.OrdinalIgnoreCase))
                    {
                        var processName = app.ProcessName
                            ?? (app.ExecutableName is null ? null : Path.GetFileNameWithoutExtension(app.ExecutableName));
                        if (processName is not null && _process.IsRunning(processName))
                        {
                            if (!_process.Kill(processName))
                                throw new InvalidOperationException(
                                    $"не удалось остановить процесс «{processName}» — остановите его вручную и повторите");
                        }
                        Directory.Delete(target, recursive: true);
                        Log.Information("uninstalled {App}: removed {Dir}", app.Name, target);
                    }
                    else
                    {
                        Log.Information("uninstall of {App}: directory {Dir} is outside AppWatch's managed root — files kept, tracking removed",
                            app.Name, target);
                    }
                    break;
                }
                default:
                    // Installer/External kinds: their uninstallers own the files; we only untrack.
                    Log.Information("uninstall of {App}: kind {Kind} — files kept, tracking removed", app.Name, app.UpdateKind);
                    break;
            }

            return new UpdateHistoryEntry(app.Name, from, "uninstalled", DateTimeOffset.UtcNow, true, null);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "uninstall failed for {App}", app.Name);
            return new UpdateHistoryEntry(app.Name, from, "uninstalled", DateTimeOffset.UtcNow, false, ex.Message);
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

        // P0 follow-up: strip Mark-of-the-Web AFTER integrity verification — the download
        // is now trusted (hash matches the GitHub digest), so Windows SmartScreen must not
        // ambush the user on the first launch. Equivalent of right-click → Unblock.
        // Single-file portable exes carry the zone ADS straight into the install dir;
        // archive entries never inherit it (that's why zip-portables launch cleanly).
        try { File.Delete(path + ":Zone.Identifier"); }
        catch (IOException) { /* no ADS present */ }
        catch (UnauthorizedAccessException) { /* best effort */ }

        return path;
    }

    private static string Sanitize(string tag) =>
        string.Concat(tag.Where(char.IsLetterOrDigit).Take(40));
}
