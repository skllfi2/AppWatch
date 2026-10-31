using AppWatch.Core.Domain;
using AppWatch.Core.Domain.Models;

namespace AppWatch.Core.Tests;

public class BackgroundCheckOrderTests
{
    private static TrackedApp App(string name, UpdateStatus? status) => new()
    {
        Name = name,
        RepoOwner = "o",
        RepoName = name,
        LastCheckStatus = status,
    };

    [Fact]
    public void Prioritize_KnownUpdateFirst_ThenSettled_ThenNeverChecked()
    {
        var apps = new[]
        {
            App("fresh-never-checked", null),          // tier 3
            App("up-to-date-old", UpdateStatus.UpToDate),          // tier 2
            App("pending-update", UpdateStatus.UpdateAvailable),  // tier 1
            App("fresh-never-checked-2", null),                    // tier 3
            App("error-old", UpdateStatus.Error),                 // tier 2
            App("pending-update-2", UpdateStatus.UpdateAvailable), // tier 1
        };

        var ordered = BackgroundCheckOrder.Prioritize(apps);

        Assert.Equal(
        [
            "pending-update", "pending-update-2",      // tier 1: actionable first
            "up-to-date-old", "error-old",              // tier 2: settled state
            "fresh-never-checked", "fresh-never-checked-2", // tier 3: новинки last
        ], ordered.Select(a => a.Name).ToArray());
    }

    [Fact]
    public void Prioritize_StableWithinTier()
    {
        var apps = new[]
        {
            App("settled-b", UpdateStatus.Pinned),
            App("settled-a", UpdateStatus.UpToDate),
            App("pending-z", UpdateStatus.UpdateAvailable),
            App("pending-a", UpdateStatus.UpdateAvailable),
        };

        var ordered = BackgroundCheckOrder.Prioritize(apps);

        // original relative order preserved inside each tier
        Assert.Equal(
            ["pending-z", "pending-a", "settled-b", "settled-a"],
            ordered.Select(a => a.Name).ToArray());
    }

    [Fact]
    public void Prioritize_EmptyList_NoCrash()
        => Assert.Empty(BackgroundCheckOrder.Prioritize([]));

    [Fact]
    public void Prioritize_AllNeverChecked_OriginalOrder()
    {
        var apps = new[] { App("c", null), App("a", null), App("b", null) };

        var ordered = BackgroundCheckOrder.Prioritize(apps);

        Assert.Equal(["c", "a", "b"], ordered.Select(a => a.Name).ToArray());
    }
}
