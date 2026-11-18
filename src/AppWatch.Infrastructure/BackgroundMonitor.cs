using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AppWatch.Core.Abstractions;
using AppWatch.Core.Domain;
using AppWatch.Core.Domain.Models;
using AppWatch.Infrastructure.Config;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AppWatch.Infrastructure;

public interface IToastService
{
    /// <summary>Single summary toast: "updates available: N". No per-app toasts — the
    /// background/catalog monitors batch their findings into one notification per cycle.</summary>
    void ShowSummary(int count);
}

public sealed class BackgroundMonitor : BackgroundService
{
    private readonly IUpdateEngine _engine;
    private readonly IGitHubClient _github;
    private readonly IConfigStore _configStore;
    private readonly IToastService _toast;
    private readonly WingetService? _winget;
    private readonly SystemVersionResolver? _systemVersion;
    private readonly ILogger<BackgroundMonitor> _logger;

    public BackgroundMonitor(
        IUpdateEngine engine,
        IGitHubClient github,
        IConfigStore configStore,
        IToastService toast,
        ILogger<BackgroundMonitor> logger,
        WingetService? winget = null,
        SystemVersionResolver? systemVersion = null)
    {
        _engine = engine;
        _github = github;
        _configStore = configStore;
        _toast = toast;
        _logger = logger;
        _winget = winget;
        _systemVersion = systemVersion;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Initial delay: honor the persisted schedule. After any prior cycle the
        // next run is already on disk — waiting until then avoids a duplicate
        // full check 30s after launch (startup check + monitor cycle together
        // burned the GitHub rate limit). First launch ever: NextRunUtc is unset,
        // so the short default delay applies.
        var initial = TimeSpan.FromSeconds(30);
        try
        {
            var cfg = _configStore.Load();
            if (cfg.BackgroundCheckNextRunUtc is { } next)
            {
                var remaining = next - DateTimeOffset.UtcNow;
                if (remaining > TimeSpan.Zero)
                {
                    initial = remaining;
                    _logger.LogInformation("Background monitor honoring persisted schedule: next run in {Minutes:F1} min",
                        remaining.TotalMinutes);
                }
            }
        }
        catch { /* best-effort */ }

        await Task.Delay(initial, stoppingToken);

        var nextDelay = TimeSpan.FromHours(4);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var config = _configStore.Load();

                if (!config.AutoCheckEnabled)
                {
                    _logger.LogInformation("Background check skipped: auto-check disabled");
                    nextDelay = TimeSpan.FromHours(1);
                }
                else if (_github.RemainingRequests is <= 0)
                {
                    var resetIn = (_github.RateLimitResetUtc - DateTimeOffset.UtcNow) ?? TimeSpan.FromHours(1);
                    _logger.LogWarning("Background check deferred: rate limited, reset in {Minutes:F0} min",
                        resetIn.TotalMinutes);
                    nextDelay = resetIn + TimeSpan.FromMinutes(1);
                }
                else
                {
                    _logger.LogInformation("Background check started");
                    int updatesFound = 0;

                    // B9: known-pending updates first, then settled apps, новинки last —
                    // when the rate-limit headroom runs out, the actionable apps are done.
                    var ordered = BackgroundCheckOrder.Prioritize(config.TrackedApps.Where(a => a.Enabled));

                    foreach (var app in ordered)
                    {
                        if (stoppingToken.IsCancellationRequested) break;

                        // Stop the loop when we're burning the last requests; leave headroom for user-initiated checks
                        if (_github.RemainingRequests is <= 3)
                        {
                            _logger.LogWarning("Background check aborted: only {Remaining} requests left in window",
                                _github.RemainingRequests);
                            break;
                        }

                        var result = await _engine.CheckAsync(app, stoppingToken).ConfigureAwait(false);

                        // B9+P6: persist the verdict — drives the next cycle's priority and error backoff
                        BackgroundCheckOrder.RecordOutcome(app, result.Status);

                        if (result.Status == UpdateStatus.UpdateAvailable)
                        {
                            updatesFound++;

                            // P1: consent gate via the shared Core policy (default OFF)
                            if (UpdatePolicy.ShouldAutoApply(config, app))
                            {
                                await _engine.ApplyAsync(app, result, cancellationToken: stoppingToken).ConfigureAwait(false);
                            }
                        }
                    }

                    // Also check WinGet package updates if available
                    if (_winget is not null && _winget.IsAvailable)
                    {
                        try
                        {
                            var wingetUpdates = await _winget.GetUpgradesAsync(stoppingToken).ConfigureAwait(false);
                            var filtered = WingetUpgradeFilter.Filter(
                                wingetUpdates,
                                name => _systemVersion?.ResolveInstalledVersion(name));
                            
                            updatesFound += filtered.Visible.Count;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogDebug(ex, "Failed to scan winget in background monitor");
                        }
                    }

                    if (updatesFound > 0)
                    {
                        _logger.LogInformation("Background check found {Count} updates (including winget)", updatesFound);
                        _toast.ShowSummary(updatesFound);
                    }
                    else
                    {
                        _logger.LogInformation("Background check: no updates");
                    }

                    nextDelay = TimeSpan.FromHours(Math.Max(1, config.CheckIntervalHours));
                }
            }
            catch (OperationCanceledException)
            {
                // Expected on shutdown
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Background check failed");
            }

            // C12: persist BackgroundCheckNextRunUtc to disk so UI schedule display + next restart
            // know when the next cycle is due without waiting for another run.
            try
            {
                var fresh = _configStore.Load();
                fresh.BackgroundCheckNextRunUtc = DateTimeOffset.UtcNow + nextDelay;
                fresh.LastCheckUtc = DateTimeOffset.UtcNow;
                _configStore.Save(fresh);
            }
            catch { /* best-effort */ }

            try
            {
                await Task.Delay(nextDelay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
