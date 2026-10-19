using AppWatch.Core.Domain;
using AppWatch.Core.Domain.Models;
using Xunit;

namespace AppWatch.Core.Tests;

/// <summary>
/// E7 (oct18): end-to-end — REAL release tag sequences from catalog repos
/// (`gh api repos/<owner>/<repo>/releases`, 2026-10-19) driven through
/// UpdateDecisionEngine.Evaluate. Proves the D1 parser fixes and D2 filter change
/// engine decisions, not just parse results. Hermetic: tags baked inline.
/// </summary>
public class LiveReleaseSequenceTests
{
    private static TrackedApp App(string name, string owner, string repo, string? current,
        UpdateKind kind, string exe, IReadOnlyList<string> priority) => new()
    {
        Name = name, RepoOwner = owner, RepoName = repo,
        InstallDirectory = @"C:\apps\test", ExecutableName = exe,
        CurrentVersion = current, UpdateKind = kind,
        AssetExtensionPriority = priority,
    };

    private static ReleaseInfo Release(string tag, bool preRelease = false, string asset = "app-x64.zip") => new()
    {
        TagName = tag, IsPreRelease = preRelease, PublishedAt = DateTimeOffset.UtcNow,
        Assets = [new ReleaseAsset(asset, "https://example.com/a", 100, null)],
    };

    // qbittorrent live tags (2026-10-19): release-5.2.0..5.2.4 stable, 5.3.0beta1/rc1 prereleases.
    // With IgnorePreReleases (default) the newest STABLE must win.
    [Fact]
    public void Evaluate_QbittorrentLiveSequence_IgnorePreReleases_PicksStable()
    {
        var app = App("qBittorrent", "qbittorrent", "qBittorrent", "release-5.2.3",
            UpdateKind.Installer, "qbittorrent.exe", [".exe"]);
        var releases = new[]
        {
            Release("release-5.3.0rc1", preRelease: true, "qbittorrent_5.3.0_x64_setup.exe"),
            Release("release-5.3.0beta1", preRelease: true, "qbittorrent_5.3.0_x64_setup.exe"),
            Release("release-5.2.4", asset: "qbittorrent_5.2.4_x64_setup.exe"),
            Release("release-5.2.3", asset: "qbittorrent_5.2.3_x64_setup.exe"),
            Release("release-5.2.2", asset: "qbittorrent_5.2.2_x64_setup.exe"),
            Release("release-5.2.1", asset: "qbittorrent_5.2.1_x64_setup.exe"),
            Release("release-5.2.0", asset: "qbittorrent_5.2.0_x64_setup.exe"),
        };

        var result = UpdateDecisionEngine.Evaluate(app, releases, SystemArchitecture.X86_64);

        Assert.Equal(UpdateStatus.UpdateAvailable, result.Status);
        Assert.Equal("release-5.2.4", result.NewRelease?.TagName);
        Assert.Equal("qbittorrent_5.2.4_x64_setup.exe", result.ChosenAsset?.Name);
    }

    // Same live sequence with prereleases allowed: 5.3.0rc1 must beat 5.3.0beta1 and 5.2.4
    [Fact]
    public void Evaluate_QbittorrentLiveSequence_AllowPreReleases_PicksRc1()
    {
        var app = App("qBittorrent", "qbittorrent", "qBittorrent", "release-5.2.3",
            UpdateKind.Installer, "qbittorrent.exe", [".exe"]);
        app.IgnorePreReleases = false;
        var releases = new[]
        {
            Release("release-5.3.0rc1", preRelease: true, "qbittorrent_5.3.0_x64_setup.exe"),
            Release("release-5.3.0beta1", preRelease: true, "qbittorrent_5.3.0_x64_setup.exe"),
            Release("release-5.2.4", asset: "qbittorrent_5.2.4_x64_setup.exe"),
        };

        var result = UpdateDecisionEngine.Evaluate(app, releases, SystemArchitecture.X86_64);

        Assert.Equal(UpdateStatus.UpdateAvailable, result.Status);
        Assert.Equal("release-5.3.0rc1", result.NewRelease?.TagName);
    }

    // Flowseal live tags: 1.9.9a..1.9.9d (letter suffixes) then 1.10.0..1.10.3.
    // From current "1.9.9d" the newest is 1.10.3 — numeric base beats any letter suffix.
    [Fact]
    public void Evaluate_FlowsealLiveSequence_FromLetterSuffix_PicksNewestNumeric()
    {
        var app = App("zapret-discord-youtube", "Flowseal", "zapret-discord-youtube", "1.9.9d",
            UpdateKind.PortableZip, "winws.exe", [".zip"]);
        var releases = new[]
        {
            Release("1.10.3", asset: "zapret-discord-youtube-1.10.3.zip"),
            Release("1.10.2", asset: "zapret-discord-youtube-1.10.2.zip"),
            Release("1.10.1", asset: "zapret-discord-youtube-1.10.1.zip"),
            Release("1.10.0", asset: "zapret-discord-youtube-1.10.0.zip"),
            Release("1.9.9d", asset: "zapret-discord-youtube-1.9.9d.zip"),
            Release("1.9.9c", asset: "zapret-discord-youtube-1.9.9c.zip"),
            Release("1.9.9b", asset: "zapret-discord-youtube-1.9.9b.zip"),
            Release("1.9.9a", asset: "zapret-discord-youtube-1.9.9a.zip"),
        };

        var result = UpdateDecisionEngine.Evaluate(app, releases, SystemArchitecture.X86_64);

        Assert.Equal(UpdateStatus.UpdateAvailable, result.Status);
        Assert.Equal("1.10.3", result.NewRelease?.TagName);
    }

    // Flowseal letter-only sequence (no 1.10.x): from "1.9.9c" the newest is 1.9.9d —
    // in-engine letter-suffix ordering, the exact case that used to be unparseable.
    [Fact]
    public void Evaluate_FlowsealLiveSequence_LetterSuffixOrdering_Picks9d()
    {
        var app = App("zapret-discord-youtube", "Flowseal", "zapret-discord-youtube", "1.9.9c",
            UpdateKind.PortableZip, "winws.exe", [".zip"]);
        var releases = new[]
        {
            Release("1.9.9a", asset: "zapret-discord-youtube-1.9.9a.zip"),
            Release("1.9.9b", asset: "zapret-discord-youtube-1.9.9b.zip"),
            Release("1.9.9c", asset: "zapret-discord-youtube-1.9.9c.zip"),
            Release("1.9.9d", asset: "zapret-discord-youtube-1.9.9d.zip"),
        };

        var result = UpdateDecisionEngine.Evaluate(app, releases, SystemArchitecture.X86_64);

        Assert.Equal(UpdateStatus.UpdateAvailable, result.Status);
        Assert.Equal("1.9.9d", result.NewRelease?.TagName);
    }

    // rustdesk live: "nightly" is published as a regular release AFTER 1.5.0 — it must
    // not shadow the newest parseable version when ordering releases.
    [Fact]
    public void Evaluate_RustDeskNightly_DoesNotShadowRealVersion()
    {
        var app = App("RustDesk", "rustdesk", "rustdesk", "1.4.9",
            UpdateKind.Installer, "rustdesk.exe", [".exe"]);
        var releases = new[]
        {
            Release("nightly", asset: "rustdesk-nightly-x86_64.exe"),
            Release("1.5.0", asset: "rustdesk-1.5.0-x86_64.exe"),
            Release("1.4.9", asset: "rustdesk-1.4.9-x86_64.exe"),
        };

        var result = UpdateDecisionEngine.Evaluate(app, releases, SystemArchitecture.X86_64);

        Assert.Equal(UpdateStatus.UpdateAvailable, result.Status);
        Assert.Equal("1.5.0", result.NewRelease?.TagName);
        Assert.Equal("rustdesk-1.5.0-x86_64.exe", result.ChosenAsset?.Name);
    }

    // yt-dlp live date-based tags: 2026.07.04 → 2026.08.19 must order chronologically
    [Fact]
    public void Evaluate_YtDlpDateTags_OrderChronologically()
    {
        var app = App("yt-dlp", "yt-dlp", "yt-dlp", "2026.07.04",
            UpdateKind.PortableExe, "yt-dlp.exe", [".exe"]);
        var releases = new[]
        {
            Release("2026.08.19", asset: "yt-dlp.exe"),
            Release("2026.07.04", asset: "yt-dlp.exe"),
            Release("2026.06.09", asset: "yt-dlp.exe"),
        };

        var result = UpdateDecisionEngine.Evaluate(app, releases, SystemArchitecture.X86_64);

        Assert.Equal(UpdateStatus.UpdateAvailable, result.Status);
        Assert.Equal("2026.08.19", result.NewRelease?.TagName);

        app.CurrentVersion = "2026.08.19";
        var upToDate = UpdateDecisionEngine.Evaluate(app, releases, SystemArchitecture.X86_64);
        Assert.Equal(UpdateStatus.UpToDate, upToDate.Status);
    }
}
