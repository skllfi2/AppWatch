using AppWatch.Core.Domain;
using Xunit;

namespace AppWatch.Core.Tests;

/// <summary>
/// D1 (oct18): VersionParser vs REAL tag formats found in catalog repos via
/// `gh api repos/<owner>/<repo>/releases --jq '.[].tag_name'` (2026-10-19).
/// </summary>
public class VersionTagFormatsTests
{
    // === E1: bare letter suffix (Flowseal/zapret-discord-youtube live tags
    // 1.9.8, 1.9.8b, 1.9.8c, 1.9.9a..1.9.9d, 1.10.0 — letter = patch bump) ===

    // Flowseal/zapret-discord-youtube: real tag "1.9.9d" — numeric base + bare letter
    [Fact]
    public void TryParse_BareLetterSuffix_ParsesBaseAndSuffix()
    {
        var ok = VersionParser.TryParse("1.9.9d", out var v);

        Assert.True(ok);
        Assert.NotNull(v);
        Assert.Equal(1, v.Major);
        Assert.Equal(9, v.Minor);
        Assert.Equal(9, v.Patch);
        Assert.Equal(0, v.Revision);
        Assert.Null(v.PreRelease);
        Assert.Equal("d", v.LetterSuffix);
    }

    // Flowseal live order: "1.9.8" → "1.9.8b" → "1.9.8c" — suffix is NEWER than plain base
    [Fact]
    public void IsNewer_BareLetterSuffix_IsNewerThanPlainBase()
    {
        Assert.True(VersionParser.IsNewer("1.9.8", "1.9.8b"));
        Assert.True(VersionParser.IsNewer("1.9.9", "1.9.9a"));
    }

    // Flowseal live order: 1.9.9a → 1.9.9b → 1.9.9c → 1.9.9d — alphabetical within same base
    [Fact]
    public void IsNewer_BareLetterSuffix_OrdersAlphabetically()
    {
        Assert.True(VersionParser.IsNewer("1.9.9a", "1.9.9b"));
        Assert.True(VersionParser.IsNewer("1.9.9b", "1.9.9c"));
        Assert.True(VersionParser.IsNewer("1.9.9c", "1.9.9d"));
        Assert.False(VersionParser.IsNewer("1.9.9d", "1.9.9c"));
    }

    // Flowseal live order: 1.9.9d → 1.10.0 — letter suffix never beats the next numeric bump
    [Fact]
    public void IsNewer_BareLetterSuffix_BelowNextNumericVersion()
    {
        Assert.True(VersionParser.IsNewer("1.9.9d", "1.10.0"));
        Assert.False(VersionParser.IsNewer("1.10.0", "1.9.9d"));
    }

    // Full Flowseal live release chain must order strictly ascending
    [Fact]
    public void IsNewer_BareLetterSuffix_FullLiveChainIsStrictlyAscending()
    {
        string[] chain =
        [
            "1.9.8", "1.9.8b", "1.9.8c", "1.9.9", "1.9.9a", "1.9.9b", "1.9.9c", "1.9.9d",
            "1.10.0", "1.10.1", "1.10.2", "1.10.3",
        ];

        for (var i = 1; i < chain.Length; i++)
        {
            Assert.True(VersionParser.IsNewer(chain[i - 1], chain[i]),
                $"expected {chain[i - 1]} < {chain[i]}");
        }
    }

    // === E2: prerelease keyword WITHOUT dash (qbittorrent/qBittorrent live tags
    // release-5.3.0rc1, release-5.3.0beta1 — GitHub marks both as prerelease) ===

    // qbittorrent/qBittorrent: real tag "release-5.3.0rc1" — rc suffix glued to the version
    [Fact]
    public void TryParse_KeywordSuffixWithoutDash_ParsesAsPreRelease()
    {
        var ok = VersionParser.TryParse("release-5.3.0rc1", out var v);

        Assert.True(ok);
        Assert.NotNull(v);
        Assert.Equal(5, v.Major);
        Assert.Equal(3, v.Minor);
        Assert.Equal(0, v.Patch);
        Assert.Equal("rc1", v.PreRelease);
        Assert.Null(v.LetterSuffix);
    }

    // qbittorrent live: 5.3.0rc1 is a prerelease of 5.3.0 → stable must rank above it
    [Fact]
    public void IsNewer_KeywordSuffix_BelowStableSameBase()
    {
        Assert.True(VersionParser.IsNewer("release-5.3.0rc1", "release-5.3.0"));
        Assert.False(VersionParser.IsNewer("release-5.3.0", "release-5.3.0rc1"));
    }

    // qbittorrent live publish order: beta1 first, then rc1 → beta1 < rc1; both above 5.2.4
    [Fact]
    public void IsNewer_KeywordSuffix_BetaBeforeRc_AbovePreviousStable()
    {
        Assert.True(VersionParser.IsNewer("release-5.3.0beta1", "release-5.3.0rc1"));
        Assert.True(VersionParser.IsNewer("release-5.2.4", "release-5.3.0beta1"));
    }

    // rustdesk/rustdesk live tag "nightly" — a release WITHOUT a version must stay unparseable,
    // otherwise it would shadow the newest real version (1.5.0) in engine ordering.
    [Fact]
    public void TryParse_NonVersionTag_Nightly_Rejected()
        => Assert.False(VersionParser.TryParse("nightly", out _));

    // obsproject/obs-studio live tag "33.0.0-rc2" — dash form must keep working after E2
    [Fact]
    public void TryParse_DashPreRelease_StillParses()
    {
        var ok = VersionParser.TryParse("33.0.0-rc2", out var v);

        Assert.True(ok);
        Assert.NotNull(v);
        Assert.Equal((33, 0, 0), (v.Major, v.Minor, v.Patch));
        Assert.Equal("rc2", v.PreRelease);
        Assert.Null(v.LetterSuffix);
    }

    // === E3: word prefixes named in RESEARCH.md D1 ("rel/v2.0", "version-0.5.0",
    // "build-123") — "version-" was previously eaten by the 'v' trim and never stripped ===

    // RESEARCH.md D1 named format: "rel/v2.0" (also seen on GitHub as "rel/")
    [Fact]
    public void TryParse_RelSlashPrefix_StripsPrefixAndV()
    {
        var ok = VersionParser.TryParse("rel/v2.0", out var v);

        Assert.True(ok);
        Assert.NotNull(v);
        Assert.Equal((2, 0, 0), (v.Major, v.Minor, v.Patch));
    }

    // RESEARCH.md D1 named format: "version-0.5.0" — must not lose its 'v' to the v-trim
    [Fact]
    public void TryParse_VersionPrefix_StripsPrefix()
    {
        var ok = VersionParser.TryParse("version-0.5.0", out var v);

        Assert.True(ok);
        Assert.NotNull(v);
        Assert.Equal((0, 5, 0), (v.Major, v.Minor, v.Patch));
    }

    // RESEARCH.md D1 named format: "build-123" — single build number parses as major
    [Fact]
    public void TryParse_BuildPrefix_SingleNumberParsesAsMajor()
    {
        var ok = VersionParser.TryParse("build-123", out var v);

        Assert.True(ok);
        Assert.NotNull(v);
        Assert.Equal(123, v.Major);
        Assert.Equal(0, v.Minor);
    }

    // qbittorrent live tag "release-5.2.4" — the oct17 prefix must keep working after reorder
    [Fact]
    public void TryParse_ReleasePrefix_StillStrips()
    {
        var ok = VersionParser.TryParse("release-5.2.4", out var v);

        Assert.True(ok);
        Assert.NotNull(v);
        Assert.Equal((5, 2, 4), (v.Major, v.Minor, v.Patch));
    }

    // Vencord/Vesktop live "v1.6.7" + microsoft/PowerToys live "v0.102.2883.0":
    // plain 'v' prefix and 4-part version must survive the prefix reorder
    [Fact]
    public void TryParse_VPrefixAndFourPart_StillParse()
    {
        Assert.True(VersionParser.TryParse("v1.6.7", out var a));
        Assert.NotNull(a);
        Assert.Equal((1, 6, 7), (a.Major, a.Minor, a.Patch));

        Assert.True(VersionParser.TryParse("v0.102.2883.0", out var b));
        Assert.NotNull(b);
        Assert.Equal((0, 102, 2883, 0), (b.Major, b.Minor, b.Patch, b.Revision));
    }
}
