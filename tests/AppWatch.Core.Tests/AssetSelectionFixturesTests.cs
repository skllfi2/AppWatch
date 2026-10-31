using AppWatch.Core.Domain;
using AppWatch.Core.Domain.Models;
using AppWatch.Core.Domain.AssetSelection;
using Xunit;

namespace AppWatch.Core.Tests;

/// <summary>
/// P4: UpdateDecisionEngine.Evaluate fixtures with REAL asset lists from catalog repos (gh api 2026-10-17).
/// Each [Fact] tests win-x64 portable asset selection for one catalog entry.
/// Expected asset hand-verified; one-line justification comment per test.
/// </summary>
public class AssetSelectionFixturesTests
{
    private static TrackedApp App(string name, string owner, string repo, UpdateKind kind,
        string currentVersion, string? executableName = null, string[]? assetPriority = null,
        string? wingetId = null, UpdateStrategy strategy = UpdateStrategy.Auto)
    {
        return new TrackedApp
        {
            Name = name,
            RepoOwner = owner,
            RepoName = repo,
            UpdateKind = kind,
            CurrentVersion = currentVersion,
            ExecutableName = executableName,
            AssetExtensionPriority = assetPriority,
            WingetPackageId = wingetId,
            Strategy = strategy,
            Enabled = true,
        };
    }

    private static ReleaseInfo Release(string tag, params ReleaseAsset[] assets)
        => new() { TagName = tag, IsPreRelease = false, PublishedAt = DateTimeOffset.UtcNow, Assets = assets };

    private static ReleaseAsset Asset(string name, long size = 1000000)
        => new(name, $"https://example.com/{name}", size, null);

    private static UpdateCheckResult Evaluate(TrackedApp app, ReleaseInfo release)
        => UpdateDecisionEngine.Evaluate(app, [release], SystemArchitecture.X86_64);

    // 1. Flowseal/zapret-discord-youtube: markerless .zip/.tar.gz/.rar → priority picks .zip
    [Fact]
    public void AssetSelect_ZapretDiscordYoutube_PicksZipByPriority()
    {
        var app = App("zapret-discord-youtube", "Flowseal", "zapret-discord-youtube",
            UpdateKind.PortableZip, "1.10.2", assetPriority: [".zip", ".tar.gz", ".rar"]);
        var rel = Release("1.10.3",
            Asset("zapret-discord-youtube-1.10.3.rar", 500000),
            Asset("zapret-discord-youtube-1.10.3.tar.gz", 600000),
            Asset("zapret-discord-youtube-1.10.3.zip", 400000));
        var result = Evaluate(app, rel);
        Assert.Equal(UpdateStatus.UpdateAvailable, result.Status);
        Assert.Equal("zapret-discord-youtube-1.10.3.zip", result.ChosenAsset?.Name); // markerless=universal, priority .zip first
    }

    // 2. Dunterbabochka/zapret2-next: only .zip asset
    [Fact]
    public void AssetSelect_Zapret2Next_OnlyZipAssetChosen()
    {
        var app = App("zapret2-next", "Dunterbabochka", "zapret2-next",
            UpdateKind.PortableZip, "v0.4.3", assetPriority: [".zip"]);
        var rel = Release("v0.4.4",
            Asset("release-sha256.txt", 1000),
            Asset("zapret2-next-v0.4.4.zip", 800000));
        var result = Evaluate(app, rel);
        Assert.Equal(UpdateStatus.UpdateAvailable, result.Status);
        Assert.Equal("zapret2-next-v0.4.4.zip", result.ChosenAsset?.Name); // only .zip asset
    }

    // 3. yt-dlp/yt-dlp: portableExe, markerless yt-dlp.exe wins over x86/arm64 variants
    [Fact]
    public void AssetSelect_YtDlp_MarkerlessExeWinsOverArchSpecific()
    {
        var app = App("yt-dlp", "yt-dlp", "yt-dlp",
            UpdateKind.PortableExe, "2026.07.15", executableName: "yt-dlp.exe", assetPriority: [".exe"]);
        var rel = Release("2026.08.19",
            Asset("yt-dlp.exe", 4000000),
            Asset("yt-dlp_x86.exe", 3500000),
            Asset("yt-dlp_arm64.exe", 3800000),
            Asset("yt-dlp_win.zip", 5000000),
            Asset("yt-dlp_win_arm64.zip", 5200000));
        var result = Evaluate(app, rel);
        Assert.Equal(UpdateStatus.UpdateAvailable, result.Status);
        Assert.Equal("yt-dlp.exe", result.ChosenAsset?.Name); // markerless=universal (P0 fix) beats x86/arm64
    }

    // 4. Vencord/Vesktop: portableZip, Vesktop-1.6.7-win.zip (win-x64) beats arm64
    [Fact]
    public void AssetSelect_Vesktop_WinX64ZipBeatsArm64()
    {
        var app = App("Vesktop", "Vencord", "Vesktop",
            UpdateKind.PortableZip, "v1.6.6", executableName: "Vesktop.exe", assetPriority: [".zip"]);
        var rel = Release("v1.6.7",
            Asset("Vesktop-1.6.7-win.zip", 120000000),
            Asset("Vesktop-1.6.7-arm64-win.zip", 115000000),
            Asset("Vesktop-Setup-1.6.7.exe", 130000000));
        var result = Evaluate(app, rel);
        Assert.Equal(UpdateStatus.UpdateAvailable, result.Status);
        Assert.Equal("Vesktop-1.6.7-win.zip", result.ChosenAsset?.Name); // portableZip strategy filters .exe; win.zip universal beats arm64
    }

    // 5. amnezia-vpn/amnezia-client: installer, windows_x64.exe exact arch match
    [Fact]
    public void AssetSelect_AmneziaVPN_WindowsX64ExeExactMatch()
    {
        var app = App("AmneziaVPN", "amnezia-vpn", "amnezia-client",
            UpdateKind.Installer, "5.0.2.0", executableName: "AmneziaVPN.exe", assetPriority: [".exe"]);
        var rel = Release("5.0.3.0",
            Asset("AmneziaVPN_5.0.3.0_windows_x64.exe", 90000000),
            Asset("AmneziaVPN_5.0.3.0_linux_x64.run", 85000000),
            Asset("AmneziaVPN_5.0.3.0_macos_x64.pkg", 88000000));
        var result = Evaluate(app, rel);
        Assert.Equal(UpdateStatus.UpdateAvailable, result.Status);
        Assert.Equal("AmneziaVPN_5.0.3.0_windows_x64.exe", result.ChosenAsset?.Name); // x64 exact match for X86_64
    }

    // 6. qbittorrent/qBittorrent: installer, x64_setup.exe (non-lt20 primary)
    [Fact]
    public void AssetSelect_Qbittorrent_X64SetupExePrimary()
    {
        var app = App("qBittorrent", "qbittorrent", "qBittorrent",
            UpdateKind.Installer, "release-5.2.3", executableName: "qbittorrent.exe", assetPriority: [".exe"]);
        var rel = Release("release-5.2.4",
            Asset("qbittorrent_5.2.4_x64_setup.exe", 45000000),
            Asset("qbittorrent_5.2.4_lt20_x64_setup.exe", 44000000),
            Asset("qbittorrent-5.2.4.tar.xz", 15000000));
        var result = Evaluate(app, rel);
        Assert.Equal(UpdateStatus.UpdateAvailable, result.Status);
        Assert.Equal("qbittorrent_5.2.4_x64_setup.exe", result.ChosenAsset?.Name); // non-lt20 is primary release, larger size
    }

    // 7. itsfatduck/optimizerDuck: portableExe, Windows-x64.exe exact match
    [Fact]
    public void AssetSelect_OptimizerDuck_WindowsX64ExeExactMatch()
    {
        var app = App("optimizerDuck", "itsfatduck", "optimizerDuck",
            UpdateKind.PortableExe, "v2.28.0", executableName: "optimizerDuck.exe", assetPriority: [".exe"]);
        var rel = Release("v2.29.0",
            Asset("optimizerDuck-Windows-x64-2.29.0.exe", 5000000),
            Asset("optimizerDuck-Windows-arm64-2.29.0.exe", 5000000));
        var result = Evaluate(app, rel);
        Assert.Equal(UpdateStatus.UpdateAvailable, result.Status);
        Assert.Equal("optimizerDuck-Windows-x64-2.29.0.exe", result.ChosenAsset?.Name); // x64 exact match beats arm64
    }

    // 8. charlie754/Game-Optimizer: portableZip, only x64.zip asset
    [Fact]
    public void AssetSelect_GameOptimizer_OnlyX64ZipAsset()
    {
        var app = App("GameOptimizer", "charlie754", "Game-Optimizer-CPUs-Threads-Optimizer",
            UpdateKind.PortableZip, "v0.5.8", executableName: "GameOptimizer.exe", assetPriority: [".zip"]);
        var rel = Release("v0.5.9",
            Asset("GameOptimizer-v0.5.9-x64.zip", 10000000));
        var result = Evaluate(app, rel);
        Assert.Equal(UpdateStatus.UpdateAvailable, result.Status);
        Assert.Equal("GameOptimizer-v0.5.9-x64.zip", result.ChosenAsset?.Name); // single x64 asset
    }

    // 9. pbatard/rufus: portableExe, markerless rufus-4.15.exe wins over rufus-4.15p.exe (name tie-break)
    [Fact]
    public void AssetSelect_Rufus_MarkerlessExeWinsTieBreak()
    {
        var app = App("Rufus", "pbatard", "rufus",
            UpdateKind.PortableExe, "v4.14", executableName: "rufus.exe", assetPriority: [".exe"]);
        var rel = Release("v4.15",
            Asset("rufus-4.15.exe", 2000000),
            Asset("rufus-4.15p.exe", 2000000),
            Asset("rufus-4.15_arm64.exe", 2000000),
            Asset("rufus-4.15_x86.exe", 1800000));
        var result = Evaluate(app, rel);
        Assert.Equal(UpdateStatus.UpdateAvailable, result.Status);
        Assert.Equal("rufus-4.15.exe", result.ChosenAsset?.Name); // both markerless=universal, same size, name ordinal wins
    }

    // 10. ventoy/Ventoy: portableZip, windows.zip markerless universal
    [Fact]
    public void AssetSelect_Ventoy_WindowsZipUniversal()
    {
        var app = App("Ventoy", "ventoy", "Ventoy",
            UpdateKind.PortableZip, "v1.1.17", executableName: "Ventoy2Disk.exe", assetPriority: [".zip"]);
        var rel = Release("v1.1.18",
            Asset("ventoy-1.1.18-windows.zip", 30000000),
            Asset("ventoy-1.1.18-linux.tar.gz", 28000000),
            Asset("ventoy-1.1.18-livecd.iso", 35000000));
        var result = Evaluate(app, rel);
        Assert.Equal(UpdateStatus.UpdateAvailable, result.Status);
        Assert.Equal("ventoy-1.1.18-windows.zip", result.ChosenAsset?.Name); // markerless=universal, .zip priority
    }

    // 11. ShareX/ShareX: portableZip, portable-x64.zip exact arch
    [Fact]
    public void AssetSelect_ShareX_PortableX64ZipExactArch()
    {
        var app = App("ShareX", "ShareX", "ShareX",
            UpdateKind.PortableZip, "v20.0.0", executableName: "ShareX.exe", assetPriority: [".zip"]);
        var rel = Release("v21.0.0",
            Asset("ShareX-21.0.0-portable-x64.zip", 25000000),
            Asset("ShareX-21.0.0-setup-x64.exe", 28000000));
        var result = Evaluate(app, rel);
        Assert.Equal(UpdateStatus.UpdateAvailable, result.Status);
        Assert.Equal("ShareX-21.0.0-portable-x64.zip", result.ChosenAsset?.Name); // portableZip strategy picks .zip; x64 exact arch
    }

    // 12. obsproject/obs-studio: portableZip, Windows-x64.zip exact arch
    [Fact]
    public void AssetSelect_OBSStudio_WindowsX64ZipExactArch()
    {
        var app = App("OBS Studio", "obsproject", "obs-studio",
            UpdateKind.PortableZip, "32.1.0", executableName: "bin\\64bit\\obs64.exe", assetPriority: [".zip"]);
        var rel = Release("32.2.2",
            Asset("OBS-Studio-32.2.2-Windows-x64.zip", 120000000),
            Asset("OBS-Studio-32.2.2-Windows-x64-Installer.exe", 130000000),
            Asset("OBS-Studio-32.2.2-macOS-Apple.dmg", 110000000));
        var result = Evaluate(app, rel);
        Assert.Equal(UpdateStatus.UpdateAvailable, result.Status);
        Assert.Equal("OBS-Studio-32.2.2-Windows-x64.zip", result.ChosenAsset?.Name); // portableZip picks .zip; x64 exact arch
    }

    // 13. rustdesk/rustdesk: installer, x86_64.exe (priority .exe over .msi)
    [Fact]
    public void AssetSelect_RustDesk_X86_64ExeOverMsi()
    {
        var app = App("RustDesk", "rustdesk", "rustdesk",
            UpdateKind.Installer, "1.4.0", executableName: "rustdesk.exe", assetPriority: [".exe"]);
        var rel = Release("1.5.0",
            Asset("rustdesk-1.5.0-x86_64.exe", 20000000),
            Asset("rustdesk-1.5.0-x86_64.msi", 22000000),
            Asset("rustdesk-1.5.0-aarch64.exe", 19000000));
        var result = Evaluate(app, rel);
        Assert.Equal(UpdateStatus.UpdateAvailable, result.Status);
        Assert.Equal("rustdesk-1.5.0-x86_64.exe", result.ChosenAsset?.Name); // .exe priority beats .msi; x86_64 exact arch
    }

    // 14. VSCodium/vscodium: portableZip, NO Windows .zip asset → NoMatchingAsset (real finding)
    [Fact]
    public void AssetSelect_VSCodium_NoWindowsZipAsset_ReturnsNoMatchingAsset()
    {
        var app = App("VSCodium", "VSCodium", "vscodium",
            UpdateKind.PortableZip, "1.139.0", executableName: "VSCodium.exe", assetPriority: [".zip"]);
        // Real latest release (1.140.06880) has only .rpm, .deb, .AppImage — no .zip for Windows
        var rel = Release("1.140.06880",
            Asset("codium_1.140.06880_amd64.deb", 90000000),
            Asset("codium-1.140.06880-el8.x86_64.rpm", 95000000),
            Asset("VSCodium-1.140.06880-anylinux-x86_64.AppImage", 110000000));
        var result = Evaluate(app, rel);
        Assert.Equal(UpdateStatus.NoMatchingAsset, result.Status); // real finding: no win-x64 portable zip published
    }

    // 15. microsoft/PowerToys: installer, Setup-x64.exe (primary over UserSetup)
    [Fact]
    public void AssetSelect_PowerToys_SetupX64ExePrimary()
    {
        var app = App("PowerToys", "microsoft", "PowerToys",
            UpdateKind.Installer, "v0.100.0", executableName: "PowerToys.exe", assetPriority: [".exe"]);
        var rel = Release("v0.101.2362.0",
            Asset("PowerToysSetup-0.101.2362.0-x64.exe", 300000000),
            Asset("PowerToysUserSetup-0.101.2362.0-x64.exe", 280000000),
            Asset("symbols-x64.zip", 50000000));
        var result = Evaluate(app, rel);
        Assert.Equal(UpdateStatus.UpdateAvailable, result.Status);
        Assert.Equal("PowerToysSetup-0.101.2362.0-x64.exe", result.ChosenAsset?.Name); // Setup larger than UserSetup, both x64 exact
    }

    // GPU-Z (TechPowerUp/GPU-Z) is wingetPackage — asset selection not applicable, skipped
}
