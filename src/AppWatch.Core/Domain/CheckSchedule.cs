namespace AppWatch.Core.Domain;

/// <summary>
/// A4 — schedule jitter: all AppWatch clients checking GitHub at exactly the same
/// moment would spike the API load (and look like an attack). ±10% random spread
/// keeps each client's interval roughly intact while desynchronizing the fleet.
/// </summary>
public static class CheckSchedule
{
    public const double JitterFraction = 0.10;

    /// <summary>Interval with ±10% random jitter (multiplicative, never below 90% / above 110%).</summary>
    public static TimeSpan WithJitter(TimeSpan interval, Random? random = null)
    {
        if (interval <= TimeSpan.Zero) return interval;

        random ??= Random.Shared;
        var factor = 1.0 + (random.NextDouble() * 2.0 - 1.0) * JitterFraction;
        return TimeSpan.FromMilliseconds(interval.TotalMilliseconds * factor);
    }
}