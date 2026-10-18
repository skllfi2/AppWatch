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
}
