using AppWatch.Core.Domain;
using AppWatch.Core.Domain.AssetSelection;
using AppWatch.Core.Domain.Models;
using Xunit;

namespace AppWatch.Core.Tests;

/// <summary>
/// D2 (oct18): non-Windows asset exclusion. Every asset name below is a REAL name from
/// `gh api repos/<owner>/<repo>/releases/tags/<tag>` (2026-10-19) unless the line
/// cites RESEARCH.md D2 as its source. Hermetic: baked inline, no network.
/// </summary>
public class PlatformAssetFilterTests
{
    [Theory]
    // rustdesk/rustdesk 1.5.0 live assets
    [InlineData("rustdesk-1.5.0-x86_64.rpm")]
    [InlineData("rustdesk-1.5.0-armv7-sciter.deb")]
    [InlineData("rustdesk-1.5.0-x86_64-signed.apk")]
    [InlineData("rustdesk-1.5.0-x86_64-sciter.flatpak")]
    [InlineData("rustdesk-1.5.0-x86_64.dmg")]
    [InlineData("rustdesk-1.5.0-x86_64.AppImage")]
    [InlineData("rustdesk-1.5.0-0-x86_64.pkg.tar.zst")]
    [InlineData("rustdesk-1.5.0-unsigned.tar.gz")]
    [InlineData("rustdesk.sbom.json")] // live: SBOM metadata, never installable
    // Vencord/Vesktop 1.6.7 live assets
    [InlineData("Vesktop-1.6.7-universal.dmg")]
    [InlineData("Vesktop-1.6.7-universal-mac.zip")] // live bug: macOS zip used to win x64 selection by size
    [InlineData("latest-mac.yml")] // live: electron-updater feed
    [InlineData("vesktop-1.6.7-arm64.tar.gz")]
    [InlineData("Vesktop-Setup-1.6.7.exe.blockmap")] // live: electron delta metadata
    [InlineData("dev.vencord.Vesktop.metainfo.xml")] // live: flatpak metainfo
    // RESEARCH.md D2 named formats with no live instance in catalog repos
    [InlineData("app-1.0.0.snap")]
    [InlineData("tool-2.0.jar")]
    public void IsWindowsCompatible_ExcludesNonWindowsAssets(string asset)
        => Assert.False(PlatformAssetFilter.IsWindowsCompatible(asset));

    [Theory]
    // Vencord/Vesktop 1.6.7 live: real Windows builds must survive the filter
    [InlineData("Vesktop-1.6.7-win.zip")]
    [InlineData("Vesktop-1.6.7-arm64-win.zip")]
    [InlineData("Vesktop-Setup-1.6.7.exe")]
    // rustdesk 1.5.0 live: Windows installers
    [InlineData("rustdesk-1.5.0-x86_64.msi")]
    [InlineData("rustdesk-1.5.0-x86-sciter.exe")]
    // Flowseal live: markerless Windows zip (P0 convention — no arch marker = universal)
    [InlineData("zapret-discord-youtube-1.10.3.zip")]
    // No recognized extension → assumed compatible (existing engine convention)
    [InlineData("tool.portable")]
    public void IsWindowsCompatible_KeepsWindowsAssets(string asset)
        => Assert.True(PlatformAssetFilter.IsWindowsCompatible(asset));

    // Vesktop live (v1.6.7): before D2, the macOS zip (224 MB) beat the real
    // Vesktop-1.6.7-win.zip (144 MB) on x64 by the size tie-break. Real sizes baked in.
    [Fact]
    public void Evaluate_VesktopLiveAssets_X64_PicksWindowsZipNotMacZip()
    {
        var app = new TrackedApp
        {
            Name = "Vesktop", RepoOwner = "Vencord", RepoName = "Vesktop",
            InstallDirectory = @"C:\apps\vesktop", ExecutableName = "Vesktop.exe",
            CurrentVersion = "v1.6.6", UpdateKind = UpdateKind.PortableZip,
            AssetExtensionPriority = [".zip"],
        };
        var rel = new ReleaseInfo
        {
            TagName = "v1.6.7", PublishedAt = DateTimeOffset.UtcNow,
            Assets =
            [
                new ReleaseAsset("Vesktop-1.6.7-win.zip", "https://example.com/w", 144162512, null),
                new ReleaseAsset("Vesktop-1.6.7-arm64-win.zip", "https://example.com/a", 141077476, null),
                new ReleaseAsset("Vesktop-1.6.7-universal-mac.zip", "https://example.com/m", 224282223, null),
                new ReleaseAsset("Vesktop-Setup-1.6.7.exe", "https://example.com/s", 199875058, null),
                new ReleaseAsset("latest.yml", "https://example.com/y", 343, null),
            ],
        };

        var result = UpdateDecisionEngine.Evaluate(app, [rel], SystemArchitecture.X86_64);

        Assert.Equal(UpdateStatus.UpdateAvailable, result.Status);
        Assert.Equal("Vesktop-1.6.7-win.zip", result.ChosenAsset?.Name);
    }

    // Vesktop live: on Windows ARM64 the arm64-win.zip exact arch match must still win
    [Fact]
    public void Evaluate_VesktopLiveAssets_Aarch64_PicksArm64WinZip()
    {
        var app = new TrackedApp
        {
            Name = "Vesktop", RepoOwner = "Vencord", RepoName = "Vesktop",
            InstallDirectory = @"C:\apps\vesktop", ExecutableName = "Vesktop.exe",
            CurrentVersion = "v1.6.6", UpdateKind = UpdateKind.PortableZip,
            AssetExtensionPriority = [".zip"],
        };
        var rel = new ReleaseInfo
        {
            TagName = "v1.6.7", PublishedAt = DateTimeOffset.UtcNow,
            Assets =
            [
                new ReleaseAsset("Vesktop-1.6.7-win.zip", "https://example.com/w", 144162512, null),
                new ReleaseAsset("Vesktop-1.6.7-arm64-win.zip", "https://example.com/a", 141077476, null),
                new ReleaseAsset("Vesktop-1.6.7-universal-mac.zip", "https://example.com/m", 224282223, null),
            ],
        };

        var result = UpdateDecisionEngine.Evaluate(app, [rel], SystemArchitecture.Aarch64);

        Assert.Equal(UpdateStatus.UpdateAvailable, result.Status);
        Assert.Equal("Vesktop-1.6.7-arm64-win.zip", result.ChosenAsset?.Name);
    }

    // rustdesk 1.5.0 live: a release offering ONLY non-Windows packages → NoMatchingAsset
    [Fact]
    public void Evaluate_OnlyNonWindowsAssets_NoMatchingAsset()
    {
        var app = new TrackedApp
        {
            Name = "RustDesk", RepoOwner = "rustdesk", RepoName = "rustdesk",
            InstallDirectory = @"C:\apps\rustdesk", ExecutableName = "rustdesk.exe",
            CurrentVersion = "1.4.9", UpdateKind = UpdateKind.Installer,
            AssetExtensionPriority = [".exe"],
        };
        var rel = new ReleaseInfo
        {
            TagName = "1.5.0", PublishedAt = DateTimeOffset.UtcNow,
            Assets =
            [
                new ReleaseAsset("rustdesk-1.5.0-x86_64.deb", "https://example.com/1", 100, null),
                new ReleaseAsset("rustdesk-1.5.0-aarch64.rpm", "https://example.com/2", 100, null),
                new ReleaseAsset("rustdesk-1.5.0-aarch64-signed.apk", "https://example.com/3", 100, null),
                new ReleaseAsset("rustdesk-1.5.0-aarch64.AppImage", "https://example.com/4", 100, null),
                new ReleaseAsset("rustdesk-1.5.0-aarch64.dmg", "https://example.com/5", 100, null),
                new ReleaseAsset("rustdesk-1.5.0-x86_64-sciter.flatpak", "https://example.com/6", 100, null),
                new ReleaseAsset("rustdesk-1.5.0-0-x86_64.pkg.tar.zst", "https://example.com/7", 100, null),
                new ReleaseAsset("rustdesk.sbom.json", "https://example.com/8", 100, null),
            ],
        };

        var result = UpdateDecisionEngine.Evaluate(app, [rel], SystemArchitecture.X86_64);

        Assert.Equal(UpdateStatus.NoMatchingAsset, result.Status);
    }
}
