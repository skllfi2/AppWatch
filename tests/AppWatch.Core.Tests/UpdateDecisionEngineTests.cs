using AppWatch.Core.Domain;
using AppWatch.Core.Domain.Models;
using Xunit;

namespace AppWatch.Core.Tests;

public class UpdateDecisionEngineTests
{
    private static TrackedApp App(Action<TrackedApp>? configure = null)
    {
        var app = new TrackedApp
        {
            Name = "Test App",
            RepoOwner = "owner",
            RepoName = "repo",
            InstallDirectory = @"C:\apps\test",
            ExecutableName = "test.exe",
            CurrentVersion = "1.0.0",
        };
        configure?.Invoke(app);
        return app;
    }

    private static ReleaseInfo Release(string tag, bool preRelease = false, ReleaseAsset[]? assets = null) => new()
    {
        TagName = tag,
        IsPreRelease = preRelease,
        PublishedAt = DateTimeOffset.UtcNow,
        Assets = assets
            ?? [new ReleaseAsset("app-x64.zip", "https://example.com/app-x64.zip", 100, null)],
    };

    [Fact]
    public void Evaluate_UpToDate_WhenCurrentEqualsNewest()
    {
        var app = App(a => a.CurrentVersion = "1.2.0");
        var releases = new[] { Release("1.2.0"), Release("1.1.0") };

        var result = UpdateDecisionEngine.Evaluate(app, releases, SystemArchitecture.X86_64);

        Assert.Equal(UpdateStatus.UpToDate, result.Status);
    }

    [Fact]
    public void Evaluate_UpdateAvailable_WhenNewerReleaseAndMatchingAsset()
    {
        var app = App(a => a.CurrentVersion = "1.0.0");
        var releases = new[] { Release("1.2.0"), Release("1.1.0") };

        var result = UpdateDecisionEngine.Evaluate(app, releases, SystemArchitecture.X86_64);

        Assert.Equal(UpdateStatus.UpdateAvailable, result.Status);
        Assert.Equal("1.2.0", result.NewRelease?.TagName);
        Assert.Equal("app-x64.zip", result.ChosenAsset?.Name);
    }

    [Fact]
    public void Evaluate_UpdateAvailable_WhenCurrentVersionUnknown()
    {
        var app = App(a => a.CurrentVersion = null);
        var releases = new[] { Release("1.2.0") };

        var result = UpdateDecisionEngine.Evaluate(app, releases, SystemArchitecture.X86_64);

        Assert.Equal(UpdateStatus.UpdateAvailable, result.Status);
    }

    [Fact]
    public void Evaluate_Pinned_NeverUpdates()
    {
        var app = App(a => { a.CurrentVersion = "1.0.0"; a.PinnedVersion = "1.0.0"; });
        var releases = new[] { Release("2.0.0") };

        var result = UpdateDecisionEngine.Evaluate(app, releases, SystemArchitecture.X86_64);

        Assert.Equal(UpdateStatus.Pinned, result.Status);
    }

    [Fact]
    public void Evaluate_Disabled_WhenDisabled()
    {
        var app = App(a => a.Enabled = false);
        var releases = new[] { Release("2.0.0") };

        var result = UpdateDecisionEngine.Evaluate(app, releases, SystemArchitecture.X86_64);

        Assert.Equal(UpdateStatus.Disabled, result.Status);
    }

    [Fact]
    public void Evaluate_NoMatchingAsset_WhenOnlyIncompatibleAssets()
    {
        var app = App(a => a.CurrentVersion = "1.0.0");
        var releases = new[]
        {
            Release("2.0.0", assets: new[] { new ReleaseAsset("app-arm64.zip", "https://example.com/a.zip", 100, null) }),
        };

        var result = UpdateDecisionEngine.Evaluate(app, releases, SystemArchitecture.X86_64);

        Assert.Equal(UpdateStatus.NoMatchingAsset, result.Status);
    }

    [Fact]
    public void Evaluate_IgnorePreReleases_SkipsPrerelease()
    {
        var app = App(a => a.CurrentVersion = "1.0.0");
        var releases = new[] { Release("1.3.0-beta", preRelease: true), Release("1.2.0") };

        var result = UpdateDecisionEngine.Evaluate(app, releases, SystemArchitecture.X86_64);

        Assert.Equal(UpdateStatus.UpdateAvailable, result.Status);
        Assert.Equal("1.2.0", result.NewRelease?.TagName);
    }

    [Fact]
    public void Evaluate_AllowsPrerelease_WhenNotIgnored()
    {
        var app = App(a => { a.CurrentVersion = "1.0.0"; a.IgnorePreReleases = false; });
        var releases = new[] { Release("1.3.0-beta", preRelease: true), Release("1.2.0") };

        var result = UpdateDecisionEngine.Evaluate(app, releases, SystemArchitecture.X86_64);

        Assert.Equal(UpdateStatus.UpdateAvailable, result.Status);
        Assert.Equal("1.3.0-beta", result.NewRelease?.TagName);
    }

    [Fact]
    public void Evaluate_PortableZipPriority_PicksZipAsset()
    {
        var app = App(a => { a.CurrentVersion = "1.0.0"; a.Strategy = UpdateStrategy.PortableReplace; });
        var releases = new[]
        {
            Release("2.0.0", assets:
            [
                new ReleaseAsset("app-x64.zip", "https://example.com/app.zip", 100, null),
                new ReleaseAsset("app-x64.exe", "https://example.com/app.exe", 100, null),
            ]),
        };

        var result = UpdateDecisionEngine.Evaluate(app, releases, SystemArchitecture.X86_64);

        Assert.Equal("app-x64.zip", result.ChosenAsset?.Name);
    }

    // === A1: SkippedVersion — user explicitly skipped a specific version ===

    [Fact]
    public void Evaluate_SkippedVersion_TreatedAsUpToDate()
    {
        var app = App(a => { a.CurrentVersion = "1.0.0"; a.SkippedVersion = "1.2.0"; });
        var releases = new[] { Release("1.2.0") };

        var result = UpdateDecisionEngine.Evaluate(app, releases, SystemArchitecture.X86_64);

        Assert.Equal(UpdateStatus.UpToDate, result.Status);
        Assert.Contains("skipped", result.Detail);
    }

    [Fact]
    public void Evaluate_SkippedVersion_Ignored_WhenNewerReleaseAppears()
    {
        var app = App(a => { a.CurrentVersion = "1.0.0"; a.SkippedVersion = "1.2.0"; });
        var releases = new[] { Release("1.3.0"), Release("1.2.0") };

        var result = UpdateDecisionEngine.Evaluate(app, releases, SystemArchitecture.X86_64);

        Assert.Equal(UpdateStatus.UpdateAvailable, result.Status);
        Assert.Equal("1.3.0", result.NewRelease?.TagName);
    }

    [Fact]
    public void Evaluate_SkippedVersion_DifferentTag_StillOffersUpdate()
    {
        // Skipped "v1.2.0" (with v prefix) but release tag is "1.2.0" — same version, different tag → still offered? No:
        // comparison is case-insensitive on the exact tag string, so a differently-prefixed tag is a different string.
        // We treat tags as opaque identifiers: only an exact tag match is skipped.
        var app = App(a => { a.CurrentVersion = "1.0.0"; a.SkippedVersion = "v1.2.0"; });
        var releases = new[] { Release("1.2.0") };

        var result = UpdateDecisionEngine.Evaluate(app, releases, SystemArchitecture.X86_64);

        Assert.Equal(UpdateStatus.UpdateAvailable, result.Status);
    }

    [Fact]
    public void Evaluate_PinAndSkip_RulesResetByClearingFields()
    {
        // After ResetRules both fields are null → normal behavior restored
        var app = App(a => { a.CurrentVersion = "1.0.0"; a.SkippedVersion = null; a.PinnedVersion = null; });
        var releases = new[] { Release("1.2.0") };

        var result = UpdateDecisionEngine.Evaluate(app, releases, SystemArchitecture.X86_64);

        Assert.Equal(UpdateStatus.UpdateAvailable, result.Status);
    }

    [Fact]
    public void Evaluate_Pinned_TakesPrecedence_OverSkipped()
    {
        // If both are set (shouldn't normally happen), Pinned wins — safest behavior
        var app = App(a => { a.CurrentVersion = "1.0.0"; a.PinnedVersion = "1.0.0"; a.SkippedVersion = "2.0.0"; });
        var releases = new[] { Release("2.0.0") };

        var result = UpdateDecisionEngine.Evaluate(app, releases, SystemArchitecture.X86_64);

        Assert.Equal(UpdateStatus.Pinned, result.Status);
    }
}
