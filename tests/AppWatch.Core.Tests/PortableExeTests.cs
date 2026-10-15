using AppWatch.Core.Domain;
using AppWatch.Core.Domain.Models;
using Xunit;

namespace AppWatch.Core.Tests;

public class AssetContentTests
{
    [Theory]
    [InlineData("zapret-discord-youtube-1.10.3.zip")]
    [InlineData("GameOptimizer-v0.5.9-x64.zip")]
    [InlineData("app-portable.7z")]
    [InlineData("app-1.0.tar.gz")]
    public void IsArchive_TrueForArchives(string name) => Assert.True(AssetContent.IsArchive(name));

    [Theory]
    [InlineData("optimizerDuck-Windows-x64-2.29.0.exe")]
    [InlineData("setup.msi")]
    public void IsArchive_FalseForSingleFiles(string name) => Assert.False(AssetContent.IsArchive(name));

    [Theory]
    [InlineData("optimizerDuck-Windows-x64-2.29.0.exe")]
    [InlineData("app-setup.msi")]
    public void IsSingleExecutable_TrueForBareExecutables(string name)
        => Assert.True(AssetContent.IsSingleExecutable(name));

    [Theory]
    [InlineData("app-1.0.zip")]
    [InlineData("installer.exe.zip")]
    public void IsSingleExecutable_FalseForArchives(string name)
        => Assert.False(AssetContent.IsSingleExecutable(name));
}

public class PortableExeStrategyTests
{
    private static TrackedApp App(UpdateKind kind) => new()
    {
        Name = "test", RepoOwner = "o", RepoName = "r",
        InstallDirectory = @"C:\Apps\test", UpdateKind = kind, Strategy = UpdateStrategy.Auto,
    };

    private static readonly ReleaseInfo Release = new()
    {
        TagName = "v2.29.0", IsPreRelease = false, PublishedAt = DateTimeOffset.UtcNow,
        Assets = [new ReleaseAsset("optimizerDuck-Windows-x64-2.29.0.exe",
            "https://example.com/dl", 87_389_615, "sha256:deadbeef")],
    };

    // P0 follow-up regression: optimizerDuck ships bare per-arch .exe files — the
    // engine must route it through PortableReplace (copy-as-is), not an installer
    // or built-in updater strategy.
    [Fact]
    public void PortableExeKind_RoutesToPortableReplace()
    {
        Assert.Equal(UpdateStrategy.PortableReplace,
            UpdateDecisionEngine.DetermineStrategy(App(UpdateKind.PortableExe), Release));
    }

    [Fact]
    public void PortableExeKind_EvaluatesUpdateAvailable()
    {
        var app = App(UpdateKind.PortableExe);
        app.CurrentVersion = "v2.28.1";
        var result = UpdateDecisionEngine.Evaluate(app, [Release], SystemArchitecture.X86_64);
        Assert.Equal(UpdateStatus.UpdateAvailable, result.Status);
        Assert.Equal("optimizerDuck-Windows-x64-2.29.0.exe", result.ChosenAsset?.Name);
        Assert.Equal(UpdateStrategy.PortableReplace, result.Strategy);
    }

    [Fact]
    public void PortableZipKind_WithBareExeRelease_FallsBackToAssetDetection()
    {
        // PortableZip-kind app whose release ships only a bare .exe (kind mismatch,
        // e.g. optimizerDuck before the catalog fix): asset-based auto-detect routes
        // it to SilentInstaller — documented pre-existing behavior, NOT PortableExe.
        Assert.Equal(UpdateStrategy.SilentInstaller,
            UpdateDecisionEngine.DetermineStrategy(App(UpdateKind.PortableZip), Release));
    }
}
