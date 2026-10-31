using System;
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
    private readonly ILogger<BackgroundMonitor> _logger;

    public BackgroundMonitor(
        IUpdateEngine engine,
        IGitHubClient github,
        IConfigStore configStore,
        IToastService toast,
        ILogger<BackgroundMonitor> logger)
    {
        _engine = engine;
        _github = github;
        _configStore = configStore;
        _toast = toast;
        _logger = logger;
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
            if (_configStore.Load().BackgroundCheckNextRunUtc is { } scheduled)
            {
                var until = scheduled - DateTimeOffset.UtcNow;
                if (until > TimeSpan.Zero)
                    initial = until;
            }
        }
        catch { /* unreadable config — use the default delay */ }

        await Task.Delay(initial, stoppingToken).ConfigureAwait(false);

        while (!stoppingToken.IsCancellationRequested)
        {
            var nextDelay = TimeSpan.FromHours(1);
            AppConfig? config = null;
            try
            {
                config = _configStore.Load();
                if (!config.AutoCheckEnabled || config.TrackedApps.Count == 0)
                {
                    // finally persists next-run and waits — no double delay here
                    nextDelay = TimeSpan.FromHours(Math.Max(1, config.CheckIntervalHours));
                    continue;
                }

                // Rate-limit aware: defer the whole cycle to the window reset
                if (_github.IsRateLimited)
                {
                    var resetIn = (_github.RateLimitResetUtc - DateTimeOffset.UtcNow) ?? TimeSpan.FromHours(1);
                    _logger.LogWarning("Background check deferred: rate limited, reset in {Minutes:F0} min",
                        resetIn.TotalMinutes);
                    nextDelay = resetIn + TimeSpan.FromMinutes(1);
                    continue;
                }

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

                if (updatesFound > 0)
                {
                    _logger.LogInformation("Background check found {Count} updates", updatesFound);
                    _toast.ShowSummary(updatesFound);
                }
                else
                {
                    _logger.LogInformation("Background check: no updates");
                }

                nextDelay = TimeSpan.FromHours(Math.Max(1, config.CheckIntervalHours));
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
            finally
            {
                if (!stoppingToken.IsCancellationRequested)
                {
                    // Reuse the cycle's config instance: this persist ALSO keeps the
                    // LastCheckStatus mutations from the loop (B9) — no reload here.
                    config ??= _configStore.Load();
                    // A4: jitter the next run by ±10% so the client fleet desynchronizes
                    nextDelay = CheckSchedule.WithJitter(nextDelay);
                    config.BackgroundCheckNextRunUtc = DateTimeOffset.UtcNow.Add(nextDelay);
                    config.LastCheckUtc = DateTimeOffset.UtcNow; // footer: «Последняя проверка»
                    _configStore.Save(config);
                    await Task.Delay(nextDelay, stoppingToken).ConfigureAwait(false);
                }
            }
        }
    }
}
