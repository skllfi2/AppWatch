using AppWatch.Core.Domain;
using AppWatch.Core.Domain.Models;
using Xunit;

namespace AppWatch.Core.Tests;

public class BackoffTests
{
    private static TrackedApp App(string name, UpdateStatus? status, int consecutiveErrors = 0) => new()
    {
        Name = name,
        RepoOwner = "o",
        RepoName = name,
        LastCheckStatus = status,
        ConsecutiveErrorCount = consecutiveErrors,
    };

    // P6: B9 tier contract intact — Tier 1 (UpdateAvailable) first
    [Fact]
    public void Prioritize_UpdateAvailable_First()
    {
        var apps = new[]
        {
            App("settled", UpdateStatus.UpToDate),
            App("pending", UpdateStatus.UpdateAvailable),
            App("never", null),
        };

        var ordered = BackgroundCheckOrder.Prioritize(apps);
        Assert.Equal("pending", ordered[0].Name);
    }

    // P6: B9 tier contract intact — Tier 3 (never checked) last
    [Fact]
    public void Prioritize_NeverChecked_Last()
    {
        var apps = new[]
        {
            App("never1", null),
            App("settled", UpdateStatus.UpToDate),
            App("never2", null),
        };

        var ordered = BackgroundCheckOrder.Prioritize(apps);
        Assert.Equal("never1", ordered[^2].Name);
        Assert.Equal("never2", ordered[^1].Name);
    }

    // P6: B9 tier contract intact — stable order within tier
    [Fact]
    public void Prioritize_StableWithinTier()
    {
        var apps = new[]
        {
            App("b", UpdateStatus.Pinned),
            App("a", UpdateStatus.UpToDate),
            App("z", UpdateStatus.UpdateAvailable),
            App("y", UpdateStatus.UpdateAvailable),
        };

        var ordered = BackgroundCheckOrder.Prioritize(apps);
        Assert.Equal(["z", "y", "b", "a"], ordered.Select(x => x.Name).ToArray());
    }

    // P6: backoff — 3+ consecutive errors deprioritized to end of Tier 2
    [Fact]
    public void Prioritize_ErrorBackoff_Threshold3_Deprioritized()
    {
        var apps = new[]
        {
            App("error-low", UpdateStatus.Error, consecutiveErrors: 2),   // Tier 2 normal
            App("error-high", UpdateStatus.Error, consecutiveErrors: 3),  // Tier 2 backoff
            App("up-to-date", UpdateStatus.UpToDate),                     // Tier 2 normal
            App("pending", UpdateStatus.UpdateAvailable),                 // Tier 1
            App("never", null),                                           // Tier 3
        };

        var ordered = BackgroundCheckOrder.Prioritize(apps);
        var names = ordered.Select(x => x.Name).ToArray();

        // Tier 1 first
        Assert.Equal("pending", names[0]);
        // Tier 2 normal (up-to-date, error-low) in original order
        Assert.Equal("error-low", names[1]);
        Assert.Equal("up-to-date", names[2]);
        // Tier 2 backoff (error-high) after normal Tier 2
        Assert.Equal("error-high", names[3]);
        // Tier 3 last
        Assert.Equal("never", names[4]);
    }

    // P6: backoff — exactly 2 errors NOT deprioritized
    [Fact]
    public void Prioritize_ErrorBackoff_Threshold2_NotDeprioritized()
    {
        var apps = new[]
        {
            App("error-2", UpdateStatus.Error, consecutiveErrors: 2),
            App("up-to-date", UpdateStatus.UpToDate),
        };

        var ordered = BackgroundCheckOrder.Prioritize(apps);
        // Both in normal Tier 2, original order preserved
        Assert.Equal(["error-2", "up-to-date"], ordered.Select(x => x.Name).ToArray());
    }

    // P6: backoff — only Error status triggers backoff, not other settled statuses
    [Fact]
    public void Prioritize_ErrorBackoff_OnlyErrorStatus()
    {
        var apps = new[]
        {
            App("pinned-3", UpdateStatus.Pinned, consecutiveErrors: 3),
            App("disabled-3", UpdateStatus.Disabled, consecutiveErrors: 3),
            App("error-3", UpdateStatus.Error, consecutiveErrors: 3),
            App("up-to-date", UpdateStatus.UpToDate),
        };

        var ordered = BackgroundCheckOrder.Prioritize(apps);
        var names = ordered.Select(x => x.Name).ToArray();

        // Only error-3 goes to backoff; pinned-3 and disabled-3 stay in normal Tier 2
        // Stable order within Tier 2 normal: pinned-3, disabled-3, up-to-date (input order)
        Assert.Equal(["pinned-3", "disabled-3", "up-to-date", "error-3"], names);
    }

    // P6: backoff — never-checked apps still last (Tier 3 after backoff)
    [Fact]
    public void Prioritize_ErrorBackoff_NeverCheckedAfterBackoff()
    {
        var apps = new[]
        {
            App("error-3", UpdateStatus.Error, consecutiveErrors: 3),
            App("never", null),
        };

        var ordered = BackgroundCheckOrder.Prioritize(apps);
        Assert.Equal(["error-3", "never"], ordered.Select(x => x.Name).ToArray());
    }

    // P6: empty list no crash
    [Fact]
    public void Prioritize_Empty_NoCrash()
        => Assert.Empty(BackgroundCheckOrder.Prioritize([]));
}