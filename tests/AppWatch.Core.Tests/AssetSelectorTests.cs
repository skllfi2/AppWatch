using AppWatch.Core.Domain.AssetSelection;
using AppWatch.Core.Domain.Models;
using Xunit;

namespace AppWatch.Core.Tests;

public class AssetSelectorTests
{
    private static GithubAssetCandidate Asset(string name, long size = 100) => new(name, size);

    private static readonly SystemArchitecture X64 = SystemArchitecture.X86_64;
    private static readonly SystemArchitecture X86 = SystemArchitecture.X86;
    private static readonly SystemArchitecture Arm64 = SystemArchitecture.Aarch64;

    [Fact]
    public void Choose_InstallerPriority_PrefersMsiThenExe()
    {
        var assets = new[] { Asset("app.zip"), Asset("app.exe"), Asset("app.msi") };
        var priority = new[] { ".msi", ".exe" };

        var chosen = AssetSelector.Choose(assets, X64, priority);

        Assert.Equal("app.msi", chosen?.Name);
    }

    // P0 live-fix regression: a markerless asset name (GitHub convention: no arch marker
    // = works everywhere) must NOT be treated as foreign. Real-world case: Flowseal
    // zapret-discord-youtube-1.10.3.{zip,tar.gz,rar} — all markerless, all were dropped
    // by the engine's ArchRank>0 filter (install failed with NoMatchingAsset).
    [Fact]
    public void MatchArchitecture_MarkerlessName_IsUniversal()
    {
        Assert.IsType<AssetArchitectureMatcher.UniversalMatch>(
            AssetArchitectureMatcher.MatchArchitecture("zapret-discord-youtube-1.10.3.zip"));
        Assert.IsType<AssetArchitectureMatcher.UniversalMatch>(
            AssetArchitectureMatcher.MatchArchitecture("optimizerDuck-v1.0.rar"));
    }

    [Fact]
    public void ArchRank_MarkerlessName_HasPositiveRank()
    {
        Assert.True(AssetSelector.ArchRank("zapret-discord-youtube-1.10.3.zip", X64) > 0);
        Assert.True(AssetSelector.ArchRank("optimizerDuck-v1.0.tar.gz", Arm64) > 0);
    }

    [Fact]
    public void MatchArchitecture_ForeignArchStillDetected()
    {
        // Explicitly foreign markers must stay incompatible (inverse of the fix above).
        Assert.IsType<AssetArchitectureMatcher.KnownMatch>(
            AssetArchitectureMatcher.MatchArchitecture("app-arm64.zip"));
        Assert.Equal(0, AssetSelector.ArchRank("app-arm64.zip", X64));
        Assert.Equal(0, AssetSelector.ArchRank("app-x64.zip", Arm64));
    }

    [Fact]
    public void Choose_PortablePriority_PrefersZip()
    {
        var assets = new[] { Asset("app.exe"), Asset("app.zip") };
        var priority = new[] { ".zip", ".exe" };

        var chosen = AssetSelector.Choose(assets, X64, priority);

        Assert.Equal("app.zip", chosen?.Name);
    }

    [Fact]
    public void Choose_ExactArch_BeatsUniversal()
    {
        var assets = new[] { Asset("app-universal.zip"), Asset("app-x64.zip") };
        var priority = new[] { ".zip" };

        var chosen = AssetSelector.Choose(assets, X64, priority);

        Assert.Equal("app-x64.zip", chosen?.Name);
    }

    [Fact]
    public void Choose_ForeignArch_FallsBackToCompatible()
    {
        var assets = new[] { Asset("app-arm64.zip"), Asset("app-x86.zip") };
        var priority = new[] { ".zip" };

        var chosen = AssetSelector.Choose(assets, X64, priority);

        Assert.Equal("app-x86.zip", chosen?.Name);
    }

    [Fact]
    public void Choose_AllIncompatible_StillReturnsBestRanked()
    {
        // Choose only ranks; compatibility filtering is the caller's job (as in komi-store).
        // The engine pre-filters incompatible assets before calling Choose.
        var assets = new[] { Asset("app-arm64.zip"), Asset("app-armv7.zip") };
        var priority = new[] { ".zip" };

        var chosen = AssetSelector.Choose(assets, X64, priority);

        Assert.Equal("app-arm64.zip", chosen?.Name);
    }

    [Fact]
    public void Choose_Stable_BeatsPrereleaseAndNightly()
    {
        var assets = new[] { Asset("app-nightly.zip"), Asset("app-1.2.0.zip"), Asset("app-1.3.0-beta.zip") };
        var priority = new[] { ".zip" };

        var chosen = AssetSelector.Choose(assets, X64, priority);

        Assert.Equal("app-1.2.0.zip", chosen?.Name);
    }

    [Fact]
    public void Choose_Prerelease_BeatsNightly()
    {
        var assets = new[] { Asset("app-nightly.zip"), Asset("app-rc.1.zip") };
        var priority = new[] { ".zip" };

        var chosen = AssetSelector.Choose(assets, X64, priority);

        Assert.Equal("app-rc.1.zip", chosen?.Name);
    }

    [Fact]
    public void Choose_Debug_LosesToEverything()
    {
        var assets = new[] { Asset("app-debug.zip"), Asset("app-nightly.zip") };
        var priority = new[] { ".zip" };

        var chosen = AssetSelector.Choose(assets, X64, priority);

        Assert.Equal("app-nightly.zip", chosen?.Name);
    }

    [Fact]
    public void Choose_LargerAsset_WinsTie()
    {
        var assets = new[] { Asset("app-a.zip", 10), Asset("app-b.zip", 20) };
        var priority = new[] { ".zip" };

        var chosen = AssetSelector.Choose(assets, X64, priority);

        Assert.Equal("app-b.zip", chosen?.Name);
    }

    [Fact]
    public void ArchRank_MatchesCommonAssetNames()
    {
        Assert.Equal(3, AssetSelector.ArchRank("app-x64.zip", X64));
        Assert.Equal(3, AssetSelector.ArchRank("app-amd64.zip", X64));
        Assert.Equal(3, AssetSelector.ArchRank("app-x86_64.zip", X64));
        Assert.Equal(2, AssetSelector.ArchRank("app-universal.zip", X64));
        Assert.Equal(1, AssetSelector.ArchRank("app-x86.zip", X64));
        Assert.Equal(0, AssetSelector.ArchRank("app-arm64.zip", X64));
        Assert.Equal(3, AssetSelector.ArchRank("app-arm64.zip", Arm64));
        Assert.Equal(0, AssetSelector.ArchRank("app-x86.zip", Arm64));
        Assert.Equal(3, AssetSelector.ArchRank("app-x86.zip", X86));
    }
}
