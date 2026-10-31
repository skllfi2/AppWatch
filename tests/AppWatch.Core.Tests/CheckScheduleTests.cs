using AppWatch.Core.Domain;

namespace AppWatch.Core.Tests;

public class CheckScheduleTests
{
    [Fact]
    public void WithJitter_StaysWithinTenPercentBounds()
    {
        var interval = TimeSpan.FromHours(6);
        var random = new Random(42);

        for (var i = 0; i < 500; i++)
        {
            var jittered = CheckSchedule.WithJitter(interval, random);
            Assert.InRange(jittered,
                TimeSpan.FromHours(6 * 0.90) - TimeSpan.FromMilliseconds(1),
                TimeSpan.FromHours(6 * 1.10) + TimeSpan.FromMilliseconds(1));
        }
    }

    [Fact]
    public void WithJitter_NotAlwaysExact()
    {
        var interval = TimeSpan.FromHours(6);
        var distinct = Enumerable.Range(0, 50)
            .Select(_ => CheckSchedule.WithJitter(interval))
            .Distinct()
            .Count();
        // continuous random — 50 draws must produce more than one distinct value
        Assert.True(distinct > 1, $"expected spread, got {distinct} distinct values");
    }

    [Fact]
    public void WithJitter_ZeroOrNegativeInterval_Passthrough()
    {
        Assert.Equal(TimeSpan.Zero, CheckSchedule.WithJitter(TimeSpan.Zero));
        Assert.Equal(TimeSpan.FromSeconds(-5), CheckSchedule.WithJitter(TimeSpan.FromSeconds(-5)));
    }
}
