using AppWatch.Core.Abstractions;

namespace AppWatch.Core.Domain;

/// <summary>Outcome of a post-update health check.</summary>
public sealed record HealthCheckOutcome(bool Healthy, string? Reason);

/// <summary>
/// B6 — health-check policy: launch the freshly installed build and verify it stays
/// alive for the whole observation window BEFORE the update is committed
/// (old version discarded). On failure the caller rolls back and records the reason.
/// Pure policy over injected abstractions — fully unit-testable (injectable delay).
/// </summary>
public static class UpdateHealthCheck
{
    /// <summary>Default observation window in seconds (overridable via AppConfig.HealthCheckSeconds).</summary>
    public const int DefaultWindowSeconds = 6;

    /// <summary>
    /// Launch the updated executable and sample its process for the whole window.
    /// Healthy = the process is still running at the end of the window.
    /// No executable or process name to observe = trivially healthy (nothing to verify).
    /// </summary>
    public static async Task<HealthCheckOutcome> VerifyStartupAsync(
        string? executablePath, string? processName,
        IProcessService process, IAppLauncher launcher,
        int? windowSeconds = null,
        Func<int, CancellationToken, Task>? delay = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(executablePath) || string.IsNullOrWhiteSpace(processName))
            return new HealthCheckOutcome(true, null);

        delay ??= (ms, ct) => Task.Delay(ms, ct);
        var window = Math.Max(1, windowSeconds ?? DefaultWindowSeconds);

        launcher.Launch(executablePath);

        // startup grace: the process must appear within the first samples
        await delay(1500, cancellationToken);

        var windowMs = window * 1000;
        const int stepMs = 500;
        var seenAlive = false;
        for (var waited = 0; waited < windowMs; waited += stepMs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (process.IsRunning(processName))
                seenAlive = true;
            else if (seenAlive)
                return new HealthCheckOutcome(false, $"{processName} exited within {window}s after start");
            await delay(stepMs, cancellationToken);
        }

        if (!process.IsRunning(processName))
            return new HealthCheckOutcome(false, seenAlive
                ? $"{processName} exited within {window}s after start"
                : $"{processName} did not start within the {window}s health-check window");

        return new HealthCheckOutcome(true, null);
    }
}