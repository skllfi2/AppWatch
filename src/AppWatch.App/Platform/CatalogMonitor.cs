using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AppWatch.Catalog;
using AppWatch.Core.Abstractions;
using AppWatch.Core.Domain.Models;
using AppWatch.Infrastructure;
using Microsoft.Extensions.Logging;

namespace AppWatch.Platform;

public interface ICatalogMonitor
{
    void Start();
    void Stop();
    Task CheckCatalogAsync(CancellationToken ct = default);
}

public sealed class CatalogMonitor : ICatalogMonitor
{
    private readonly CatalogService _catalog;
    private readonly IGitHubClient _github;
    private readonly IToastService _toast;
    private readonly ILogger<CatalogMonitor> _logger;
    private Timer? _timer;
    private CancellationTokenSource? _cts;

    public CatalogMonitor(
        CatalogService catalog,
        IGitHubClient github,
        IToastService toast,
        ILogger<CatalogMonitor> logger)
    {
        _catalog = catalog;
        _github = github;
        _toast = toast;
        _logger = logger;
    }

    public void Start()
    {
        _cts = new CancellationTokenSource();
        // Initial delay: 2 min after app start
        _timer = new Timer(async _ => await CheckCatalogAsync(_cts.Token), null,
            TimeSpan.FromMinutes(2), Timeout.InfiniteTimeSpan);
    }

    public void Stop()
    {
        _cts?.Cancel();
        _timer?.Dispose();
        _timer = null;
    }

    public async Task CheckCatalogAsync(CancellationToken stoppingToken)
    {
        var nextDelay = TimeSpan.FromHours(12);
        try
        {
            if (_github.IsRateLimited)
            {
                var resetIn = (_github.RateLimitResetUtc - DateTimeOffset.UtcNow) ?? TimeSpan.FromHours(1);
                _logger.LogWarning("Catalog check deferred: rate limited, reset in {Minutes:F0} min", resetIn.TotalMinutes);
                nextDelay = resetIn + TimeSpan.FromMinutes(1);
                return;
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Catalog check failed");
        }
        finally
        {
            if (!stoppingToken.IsCancellationRequested)
            {
                _timer?.Change(nextDelay, Timeout.InfiniteTimeSpan);
            }
        }
    }
}