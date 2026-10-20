using AppWatch.Core.Domain;
using Xunit;

namespace AppWatch.Core.Tests;

/// <summary>
/// P3: VersionParser.IsNewer fixtures with REAL version strings from catalog repos.
/// Each [Fact] has a one-line justification comment citing the source repo.
/// Source: gh api repos/<owner>/<repo>/releases/latest --jq .tag_name (2026-10-17)
/// </summary>
public class VersionComparisonFixturesTests
{
    // zapret-discord-youtube (Flowseal): real tags 1.10.2 → 1.10.3
    [Fact]
    public void IsNewer_Zapret_1_10_2_to_1_10_3_True()
        => Assert.True(VersionParser.IsNewer("1.10.2", "1.10.3")); // Flowseal/zapret-discord-youtube: 1.10.2 → 1.10.3

    [Fact]
    public void IsNewer_Zapret_SameVersion_False()
        => Assert.False(VersionParser.IsNewer("1.10.3", "1.10.3")); // Flowseal/zapret-discord-youtube: same tag

    // yt-dlp: real tags 2026.07.15 → 2026.08.19 (date-based semver)
    [Fact]
    public void IsNewer_YtDlp_2026_07_15_to_2026_08_19_True()
        => Assert.True(VersionParser.IsNewer("2026.07.15", "2026.08.19")); // yt-dlp/yt-dlp: 2026.07.15 → 2026.08.19

    [Fact]
    public void IsNewer_YtDlp_SameVersion_False()
        => Assert.False(VersionParser.IsNewer("2026.08.19", "2026.08.19")); // yt-dlp/yt-dlp: same tag

    // Vesktop (Vencord): real tags v1.6.6 → v1.6.7
    [Fact]
    public void IsNewer_Vesktop_v1_6_6_to_v1_6_7_True()
        => Assert.True(VersionParser.IsNewer("v1.6.6", "v1.6.7")); // Vencord/Vesktop: v1.6.6 → v1.6.7

    [Fact]
    public void IsNewer_Vesktop_WithoutVPrefix_True()
        => Assert.True(VersionParser.IsNewer("1.6.6", "v1.6.7")); // Vencord/Vesktop: parser strips 'v' prefix

    // GameOptimizer (charlie754): real tags 0.5.8.0 → 0.5.9.0 (4-part version)
    [Fact]
    public void IsNewer_GameOptimizer_0_5_8_0_to_0_5_9_0_True()
        => Assert.True(VersionParser.IsNewer("0.5.8.0", "0.5.9.0")); // charlie754/Game-Optimizer: 0.5.8.0 → 0.5.9.0

    [Fact]
    public void IsNewer_GameOptimizer_FourPartComparison_True()
        => Assert.True(VersionParser.IsNewer("1.19.3.3218", "1.19.3.3219")); // Generic 4-part: revision bump

    // zapret2-next (Dunterbabochka): real tags v0.4.3 → v0.4.4
    [Fact]
    public void IsNewer_Zapret2Next_v0_4_3_to_v0_4_4_True()
        => Assert.True(VersionParser.IsNewer("v0.4.3", "v0.4.4")); // Dunterbabochka/zapret2-next: v0.4.3 → v0.4.4

    // optimizerDuck (itsfatduck): real tags v2.28.0 → v2.29.0
    [Fact]
    public void IsNewer_OptimizerDuck_v2_28_0_to_v2_29_0_True()
        => Assert.True(VersionParser.IsNewer("v2.28.0", "v2.29.0")); // itsfatduck/optimizerDuck: v2.28.0 → v2.29.0

    // Rufus (pbatard): real tags v4.14 → v4.15
    [Fact]
    public void IsNewer_Rufus_v4_14_to_v4_15_True()
        => Assert.True(VersionParser.IsNewer("v4.14", "v4.15")); // pbatard/rufus: v4.14 → v4.15

    // Ventoy (ventoy): real tags v1.1.17 → v1.1.18
    [Fact]
    public void IsNewer_Ventoy_v1_1_17_to_v1_1_18_True()
        => Assert.True(VersionParser.IsNewer("v1.1.17", "v1.1.18")); // ventoy/Ventoy: v1.1.17 → v1.1.18

    // ShareX: real tags v20.0.0 → v21.0.0 (major bump)
    [Fact]
    public void IsNewer_ShareX_v20_0_0_to_v21_0_0_True()
        => Assert.True(VersionParser.IsNewer("v20.0.0", "v21.0.0")); // ShareX/ShareX: v20.0.0 → v21.0.0

    // OBS Studio (obsproject): real tags 32.1.0 → 32.2.2 (no v prefix)
    [Fact]
    public void IsNewer_OBSStudio_32_1_0_to_32_2_2_True()
        => Assert.True(VersionParser.IsNewer("32.1.0", "32.2.2")); // obsproject/obs-studio: 32.1.0 → 32.2.2

    // RustDesk (rustdesk): real tags 1.4.0 → 1.5.0
    [Fact]
    public void IsNewer_RustDesk_1_4_0_to_1_5_0_True()
        => Assert.True(VersionParser.IsNewer("1.4.0", "1.5.0")); // rustdesk/rustdesk: 1.4.0 → 1.5.0

    // VSCodium: real tags 1.139.0 → 1.140.06880 (4-part build number)
    [Fact]
    public void IsNewer_VSCodium_1_139_0_to_1_140_06880_True()
        => Assert.True(VersionParser.IsNewer("1.139.0", "1.140.06880")); // VSCodium/vscodium: 1.139.0 → 1.140.06880

    // PowerToys (microsoft): real tags v0.100.0 → v0.101.2362.0 (4-part)
    [Fact]
    public void IsNewer_PowerToys_v0_100_0_to_v0_101_2362_0_True()
        => Assert.True(VersionParser.IsNewer("v0.100.0", "v0.101.2362.0")); // microsoft/PowerToys: v0.100.0 → v0.101.2362.0

    // Pre-release handling with real-world patterns
    [Fact]
    public void IsNewer_PreRelease_BetaToStable_True()
        => Assert.True(VersionParser.IsNewer("1.0.0-beta", "1.0.0")); // Generic: pre-release < stable

    [Fact]
    public void IsNewer_PreRelease_RcToStable_True()
        => Assert.True(VersionParser.IsNewer("1.0.0-rc.1", "1.0.0")); // Generic: RC < stable

    [Fact]
    public void IsNewer_PreRelease_Rc1ToRc2_True()
        => Assert.True(VersionParser.IsNewer("1.0.0-rc.1", "1.0.0-rc.2")); // Generic: RC ordering

    // Older candidate should return false (regression guard)
    [Fact]
    public void IsNewer_OlderCandidate_ReturnsFalse()
        => Assert.False(VersionParser.IsNewer("1.10.3", "1.10.2")); // Flowseal/zapret: newer current, older candidate

    [Fact]
    public void IsNewer_NullCurrent_AlwaysTrue()
        => Assert.True(VersionParser.IsNewer(null, "1.0.0")); // Unknown current → any candidate is newer

    // ── План §6.2 (версионная матрица): числовые суффиксы prerelease ──

    [Theory]
    [InlineData("1.2.0-beta.2", "1.2.0-beta.10", true)]   // numeric, NOT lexicographic (beta.10 > beta.2)
    [InlineData("1.2.0-beta.10", "1.2.0-beta.2", false)]   // reverse must be false
    [InlineData("1.2.0-beta.10", "1.2.0", true)]          // any prerelease < stable
    [InlineData("1.2.0", "1.2.0-beta.10", false)]          // stable is NOT newer than its prerelease
    [InlineData("1.2.9", "1.2.10", true)]                  // numeric segments (1.2.10 > 1.2.9)
    [InlineData("1.2.10", "1.2.9", false)]                 // reverse
    public void IsNewer_PlanMatrix_NumericPrereleaseAndSegments(string current, string candidate, bool expected)
        => Assert.Equal(expected, VersionParser.IsNewer(current, candidate));

    // Republished release: same version is never "newer" — no pointless reinstall
    [Theory]
    [InlineData("1.10.3")]
    [InlineData("v1.10.3")]
    [InlineData("1.2.0-beta.10")]
    public void IsNewer_RepublishedSameVersion_False(string version)
        => Assert.False(VersionParser.IsNewer(version, version));
}