using System;
using System.Threading;
using System.Threading.Tasks;
using AppWatch.Core.Abstractions;
using AppWatch.Core.Domain.Models;
using AppWatch.Infrastructure.Config;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AppWatch.Infrastructure;

public interface IToastService
{
    void ShowUpdateAvailable(string appName, string version);
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
            // Initial delay before first check (30s after app start)
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                var nextDelay = TimeSpan.FromHours(1);
                try
                {
                    var config = _configStore.Load();
                    if (!config.AutoCheckEnabled || config.TrackedApps.Count == 0)
                    {
                        nextDelay = TimeSpan.FromHours(Math.Max(1, config.CheckIntervalHours));
                        config.BackgroundCheckNextRunUtc = DateTimeOffset.UtcNow.Add(nextDelay);
                        _configStore.Save(config);
                        await Task.Delay(nextDelay, stoppingToken);
                        continue;
                    }

                    // Rate-limit aware: defer the whole cycle to the window reset
                    if (_github.IsRateLimited)
                    {
                        var resetIn = (_github.RateLimitResetUtc - DateTimeOffset.UtcNow) ?? TimeSpan.FromHours(1);
                        _logger.LogWarning("Background check deferred: rate limited, reset in {Minutes:F0} min",
                            resetIn.TotalMinutes);
                        nextDelay = resetIn + TimeSpan.FromMinutes(1);
                        config.BackgroundCheckNextRunUtc = DateTimeOffset.UtcNow.Add(nextDelay);
                        _configStore.Save(config);
                        await Task.Delay(nextDelay, stoppingToken);
                        continue;
                    }

                    _logger.LogInformation("Background check started");
                    int updatesFound = 0;

                    foreach (var app in config.TrackedApps)
                    {
                        if (!app.Enabled) continue;
                        if (stoppingToken.IsCancellationRequested) break;

                        // Stop the loop when we're burning the last requests; leave headroom for user-initiated checks
                        if (_github.RemainingRequests is <= 3)
                        {
                            _logger.LogWarning("Background check aborted: only {Remaining} requests left in window",
                                _github.RemainingRequests);
                            break;
                        }

                        var result = await _engine.CheckAsync(app, stoppingToken);
                        if (result.Status == UpdateStatus.UpdateAvailable)
                        {
                            updatesFound++;
                            _toast.ShowUpdateAvailable(app.Name, result.NewRelease?.TagName ?? "?");

                            // Auto-apply if enabled
                            if (config.AutoApplyUpdates && app.LaunchAfterUpdate)
                            {
                                await _engine.ApplyAsync(app, result, cancellationToken: stoppingToken);
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
                        var config = _configStore.Load();
                        config.BackgroundCheckNextRunUtc = DateTimeOffset.UtcNow.Add(nextDelay);
                        _configStore.Save(config);
                        await Task.Delay(nextDelay, stoppingToken);
                    }
                }
            }
        }
    }