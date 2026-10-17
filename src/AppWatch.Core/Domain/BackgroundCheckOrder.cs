using AppWatch.Core.Domain.Models;

namespace AppWatch.Core.Domain;

/// <summary>
/// B9 — rate-limit-aware ordering of the background check cycle.
/// Tier 1: apps with a known available update (checked before, update pending) — the
///         actionable ones, must survive any budget cut;
/// Tier 2: apps checked before with a settled state (up-to-date / error / pinned …);
///         apps with >=3 consecutive errors are deprioritized to the end of Tier 2;
/// Tier 3: новинки — never checked yet, no cached verdict — last in line.
/// Order inside a tier is stable (original config order preserved).
/// </summary>
public static class BackgroundCheckOrder
{
    private const int ErrorBackoffThreshold = 3;

    public static IReadOnlyList<TrackedApp> Prioritize(IEnumerable<TrackedApp> apps)
    {
        var tier1 = new List<TrackedApp>();
        var tier2 = new List<TrackedApp>();
        var tier2Backoff = new List<TrackedApp>();
        var tier3 = new List<TrackedApp>();

        foreach (var app in apps)
        {
            switch (app.LastCheckStatus)
            {
                case UpdateStatus.UpdateAvailable:
                    tier1.Add(app);
                    break;
                case null:
                    tier3.Add(app);
                    break;
                default:
                    // Tier 2: settled state — but deprioritize high consecutive errors
                    if (app.LastCheckStatus == UpdateStatus.Error && app.ConsecutiveErrorCount >= ErrorBackoffThreshold)
                        tier2Backoff.Add(app);
                    else
                        tier2.Add(app);
                    break;
            }
        }

        tier1.AddRange(tier2);
        tier1.AddRange(tier2Backoff);
        tier1.AddRange(tier3);
        return tier1;
    }
}