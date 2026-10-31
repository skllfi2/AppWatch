using AppWatch.Core.Domain;
using AppWatch.Core.Domain.Models;
using Xunit;

namespace AppWatch.Core.Tests;

/// <summary>
/// P2: Tracking-guarantee invariant — successful update check MUST have version + asset known.
/// Pure Core rule: UpdateStatus.UpdateAvailable ⇒ NewRelease != null ∧ ChosenAsset != null ∧ !string.IsNullOrEmpty(NewRelease.TagName).
/// </summary>
public class TrackingGuaranteeTests
{
    private static TrackedApp App(string currentVersion = "1.0.0") => new()
    {
        Name = "Test",
        RepoOwner = "o",
        RepoName = "r",
        CurrentVersion = currentVersion,
        Enabled = true,
        InstallDirectory = @"C:\apps\test",
        ExecutableName = "test.exe",
    };

    private static ReleaseInfo Release(string tag, params ReleaseAsset[] assets) => new()
    {
        TagName = tag,
        IsPreRelease = false,
        PublishedAt = DateTimeOffset.UtcNow,
        Assets = assets,
    };

    private static ReleaseAsset Asset(string name, long size = 1000000)
        => new(name, $"https://example.com/{name}", size, null);

    // P2: UpdateAvailable ⇒ NewRelease.TagName present
    [Fact]
    public void TrackingGuarantee_UpdateAvailable_HasReleaseTag()
    {
        var app = App();
        var rel = Release("2.0.0", Asset("app-x64.zip"));
        var result = UpdateDecisionEngine.Evaluate(app, [rel], SystemArchitecture.X86_64);

        Assert.Equal(UpdateStatus.UpdateAvailable, result.Status);
        Assert.NotNull(result.NewRelease);
        Assert.False(string.IsNullOrEmpty(result.NewRelease!.TagName));
    }

    // P2: UpdateAvailable ⇒ ChosenAsset present and non-empty name
    [Fact]
    public void TrackingGuarantee_UpdateAvailable_HasChosenAsset()
    {
        var app = App();
        var rel = Release("2.0.0", Asset("app-x64.zip"));
        var result = UpdateDecisionEngine.Evaluate(app, [rel], SystemArchitecture.X86_64);

        Assert.Equal(UpdateStatus.UpdateAvailable, result.Status);
        Assert.NotNull(result.ChosenAsset);
        Assert.False(string.IsNullOrEmpty(result.ChosenAsset!.Name));
    }

    // P2: UpdateAvailable ⇒ Strategy determined (not Auto)
    [Fact]
    public void TrackingGuarantee_UpdateAvailable_HasStrategy()
    {
        var app = App();
        var rel = Release("2.0.0", Asset("app-x64.zip"));
        var result = UpdateDecisionEngine.Evaluate(app, [rel], SystemArchitecture.X86_64);

        Assert.Equal(UpdateStatus.UpdateAvailable, result.Status);
        Assert.NotEqual(UpdateStrategy.Auto, result.Strategy);
    }

    // P2: NoMatchingAsset ⇒ NewRelease present but ChosenAsset null (version known, asset not)
    [Fact]
    public void TrackingGuarantee_NoMatchingAsset_HasReleaseButNoAsset()
    {
        var app = App();
        var rel = Release("2.0.0", Asset("app-arm64.zip")); // incompatible arch
        var result = UpdateDecisionEngine.Evaluate(app, [rel], SystemArchitecture.X86_64);

        Assert.Equal(UpdateStatus.NoMatchingAsset, result.Status);
        Assert.NotNull(result.NewRelease);
        Assert.False(string.IsNullOrEmpty(result.NewRelease!.TagName));
        Assert.Null(result.ChosenAsset);
    }

    // P2: UpToDate ⇒ no release/asset required (invariant only for UpdateAvailable)
    [Fact]
    public void TrackingGuarantee_UpToDate_NoInvariantRequired()
    {
        var app = App("2.0.0");
        var rel = Release("2.0.0", Asset("app-x64.zip"));
        var result = UpdateDecisionEngine.Evaluate(app, [rel], SystemArchitecture.X86_64);

        Assert.Equal(UpdateStatus.UpToDate, result.Status);
        // Invariant does not apply — NewRelease/ChosenAsset may be null
    }

    // P2: Pinned ⇒ release known (latest) but no asset (invariant: version known even if not applying)
    [Fact]
    public void TrackingGuarantee_Pinned_HasLatestReleaseInfo()
    {
        var app = App();
        app.PinnedVersion = "1.0.0";
        var rel = Release("2.0.0", Asset("app-x64.zip"));
        var result = UpdateDecisionEngine.Evaluate(app, [rel], SystemArchitecture.X86_64);

        Assert.Equal(UpdateStatus.Pinned, result.Status);
        Assert.NotNull(result.NewRelease);
        Assert.Equal("2.0.0", result.NewRelease!.TagName); // latest known
        Assert.Null(result.ChosenAsset); // not applying
    }

    // P2: Disabled ⇒ no invariant (not checked)
    [Fact]
    public void TrackingGuarantee_Disabled_NoInvariantRequired()
    {
        var app = App();
        app.Enabled = false;
        var rel = Release("2.0.0", Asset("app-x64.zip"));
        var result = UpdateDecisionEngine.Evaluate(app, [rel], SystemArchitecture.X86_64);

        Assert.Equal(UpdateStatus.Disabled, result.Status);
        Assert.Null(result.NewRelease);
        Assert.Null(result.ChosenAsset);
    }
}
