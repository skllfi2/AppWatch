using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AppWatch.Catalog;
using AppWatch.Core.Abstractions;
using AppWatch.Core.Domain;
using AppWatch.Core.Domain.Models;
using AppWatch.Infrastructure;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AppWatch.Platform;

public interface ICatalogMonitor
{
    Task CheckCatalogAsync(CancellationToken ct = default);
}

public sealed class CatalogMonitor : BackgroundService, ICatalogMonitor
{
    private readonly CatalogService _catalog;
    private readonly IGitHubClient _github;
    private readonly IConfigStore _configStore;
    private readonly IToastService _toast;
    private readonly ILogger<CatalogMonitor> _logger;

    public CatalogMonitor(
        CatalogService catalog,
        IGitHubClient github,
        IConfigStore configStore,
        IToastService toast,
        ILogger<CatalogMonitor> logger)
    {
        _catalog = catalog;
        _github = github;
        _configStore = configStore;
        _toast = toast;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Initial delay: 2 min after app start
        await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            var nextDelay = TimeSpan.FromHours(12);
            try
            {
                if (_github.IsRateLimited)
                {
                    var resetIn = (_github.RateLimitResetUtc - DateTimeOffset.UtcNow) ?? TimeSpan.FromHours(1);
                    _logger.LogWarning("Catalog check deferred: rate limited, reset in {Minutes:F0} min", resetIn.TotalMinutes);
                    nextDelay = resetIn + TimeSpan.FromMinutes(1);
                    var config = _configStore.Load();
                    config.CatalogCheckNextRunUtc = DateTimeOffset.UtcNow.Add(nextDelay);
                    _configStore.Save(config);
                    await Task.Delay(nextDelay, stoppingToken);
                    continue;
                }

                _logger.LogInformation("Catalog check started for {Count} apps", _catalog.Entries.Count);
                int updatesFound = 0;

                foreach (var entry in _catalog.Entries)
                {
                    if (stoppingToken.IsCancellationRequested) break;

                    // Respect rate limit: leave headroom for user checks
                    if (_github.RemainingRequests is <= 3)
                    {
                        _logger.LogWarning("Catalog check aborted: only {Remaining} requests left", _github.RemainingRequests);
                        break;
                    }

                    // Skip winget packages (handled by winget)
                    if (entry.UpdateKind == UpdateKind.WingetPackage) continue;

                    // Build a temporary TrackedApp for version checking
                    var app = new TrackedApp
                    {
                        Name = entry.Name,
                        RepoOwner = entry.RepoOwner,
                        RepoName = entry.RepoName,
                        UpdateKind = entry.UpdateKind,
                        IgnorePreReleases = entry.IgnorePreReleases,
                        AssetExtensionPriority = entry.AssetExtensionPriority,
                        ProcessName = entry.ProcessName,
                        ExecutableName = entry.ExecutableName,
                        CurrentVersion = null // Force fresh check
                    };

                    var releases = await _github.GetReleasesAsync(entry.RepoOwner, entry.RepoName, stoppingToken);
                    if (releases is { Count: > 0 })
                    {
                        var latest = releases[0];
                        // Compare with locally stored version (if any)
                        // For catalog, we just notify about new releases
                        updatesFound++;
                        _toast.ShowUpdateAvailable(entry.Name, latest.TagName);
                        _logger.LogInformation("Catalog update: {App} -> {Version}", entry.Name, latest.TagName);
                    }
                }

                if (updatesFound > 0)
                {
                    _toast.ShowSummary(updatesFound);
                    _logger.LogInformation("Catalog check found {Count} updates", updatesFound);
                }
                else
                {
                    _logger.LogInformation("Catalog check: no updates");
                }

                nextDelay = TimeSpan.FromHours(12);
            }
            catch (OperationCanceledException)
            {
                // Expected on shutdown
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Catalog check failed");
            }
            finally
            {
                if (!stoppingToken.IsCancellationRequested)
                {
                    var config = _configStore.Load();
                    // A4: ±10% jitter — see CheckSchedule
                    nextDelay = CheckSchedule.WithJitter(nextDelay);
                    config.CatalogCheckNextRunUtc = DateTimeOffset.UtcNow.Add(nextDelay);
                    _configStore.Save(config);
                    await Task.Delay(nextDelay, stoppingToken);
                }
            }
        }
    }

    public async Task CheckCatalogAsync(CancellationToken ct = default)
    {
        // For manual checks (e.g., from tray menu), run one iteration immediately
        // This is a simplified version without the delay/retry logic
        try
        {
            if (_github.IsRateLimited)
            {
                _logger.LogWarning("Manual catalog check skipped: rate limited");
                return;
            }

            _logger.LogInformation("Manual catalog check started for {Count} apps", _catalog.Entries.Count);
            int updatesFound = 0;

            foreach (var entry in _catalog.Entries)
            {
                if (ct.IsCancellationRequested) break;
                if (_github.RemainingRequests is <= 3) break;
                if (entry.UpdateKind == UpdateKind.WingetPackage) continue;

                var app = new TrackedApp
                {
                    Name = entry.Name,
                    RepoOwner = entry.RepoOwner,
                    RepoName = entry.RepoName,
                    UpdateKind = entry.UpdateKind,
                    IgnorePreReleases = entry.IgnorePreReleases,
                    AssetExtensionPriority = entry.AssetExtensionPriority,
                    ProcessName = entry.ProcessName,
                    ExecutableName = entry.ExecutableName,
                    CurrentVersion = null
                };

                var releases = await _github.GetReleasesAsync(entry.RepoOwner, entry.RepoName, ct);
                if (releases is { Count: > 0 })
                {
                    var latest = releases[0];
                    updatesFound++;
                    _toast.ShowUpdateAvailable(entry.Name, latest.TagName);
                    _logger.LogInformation("Manual catalog update: {App} -> {Version}", entry.Name, latest.TagName);
                }
            }

            if (updatesFound > 0)
            {
                _toast.ShowSummary(updatesFound);
                _logger.LogInformation("Manual catalog check found {Count} updates", updatesFound);
            }
            else
            {
                _logger.LogInformation("Manual catalog check: no updates");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Manual catalog check failed");
        }
    }
}