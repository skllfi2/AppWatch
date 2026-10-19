using AppWatch.Core.Domain;
using Xunit;

namespace AppWatch.Core.Tests;

/// <summary>
/// D3 (oct18): CatalogSearch.Score fuzzy matching. Names/descriptions are REAL
/// catalog entries (src/AppWatch.App/Catalog/catalog.json, read-only source);
/// the three core cases are named in RESEARCH.md D3. Hermetic: baked inline.
/// </summary>
public class CatalogSearchTests
{
    // Real catalog entries (catalog.json)
    private const string YtDlpName = "yt-dlp";
    private const string YtDlpDesc = "Загрузчик YouTube: видео, аудио, плейлисты (обновляется еженедельно)";
    private const string QbittorrentDesc = "Торрент-клиент без рекламы (стандарт де-факто)";

    // RESEARCH.md D3 case 1: "ytdl" should find "yt-dlp" (missing dash + cropped name)
    [Fact]
    public void Score_Ytdl_FindsYtDlp()
        => Assert.True(CatalogSearch.Score("ytdl", YtDlpName, YtDlpDesc) > 0);

    // RESEARCH.md D3 case 2: "vescop" (typo of Vesktop, Levenshtein 2) should find it
    [Fact]
    public void Score_VescopTypo_FindsVesktop()
    {
        var score = CatalogSearch.Score("vescop", "Vesktop", "Discord-клиент с Vencord внутри");

        Assert.Equal(CatalogSearch.ScoreTypo, score);
    }

    // RESEARCH.md D3 case 3: "optimizer" should find BOTH optimizerDuck and GameOptimizer
    [Fact]
    public void Score_Optimizer_FindsBothOptimizerDuckAndGameOptimizer()
    {
        var duck = CatalogSearch.Score("optimizer", "optimizerDuck", "Бесплатный оптимизатор Windows");
        var game = CatalogSearch.Score("optimizer", "GameOptimizer", "Оптимизатор игр");

        // duck: name starts with the query (prefix tier); game: query inside the name
        Assert.Equal(CatalogSearch.ScorePrefix, duck);
        Assert.Equal(CatalogSearch.ScoreSubstring, game);
    }

    // Ranking contract on the real name "yt-dlp": each tier strictly below the previous
    [Fact]
    public void Score_TiersRank_Exact_Prefix_Substring_Typo_Subsequence_Description()
    {
        var exact = CatalogSearch.Score("yt-dlp", YtDlpName, YtDlpDesc);
        var prefix = CatalogSearch.Score("yt", YtDlpName, YtDlpDesc);
        var substring = CatalogSearch.Score("t-dl", YtDlpName, YtDlpDesc);
        var typo = CatalogSearch.Score("ytdlp", YtDlpName, YtDlpDesc);
        var subsequence = CatalogSearch.Score("ylp", YtDlpName, YtDlpDesc);
        var description = CatalogSearch.Score("аудио", YtDlpName, YtDlpDesc);
        var none = CatalogSearch.Score("zzz", YtDlpName, YtDlpDesc);

        Assert.Equal(CatalogSearch.ScoreExact, exact);
        Assert.Equal(CatalogSearch.ScorePrefix, prefix);
        Assert.Equal(CatalogSearch.ScoreSubstring, substring);
        Assert.Equal(CatalogSearch.ScoreTypo, typo);
        Assert.Equal(CatalogSearch.ScoreSubsequence, subsequence);
        Assert.Equal(CatalogSearch.ScoreDescription, description);
        Assert.Equal(0, none);
        Assert.True(exact > prefix && prefix > substring && substring > typo
            && typo > subsequence && subsequence > description && description > 0);
    }

    // A typo must never outrank the exact name: real Vesktop entry
    [Fact]
    public void Score_Typo_NeverBeatsExactName()
    {
        var typo = CatalogSearch.Score("veskop", "Vesktop", "Discord-клиент с Vencord внутри");
        var exact = CatalogSearch.Score("vesktop", "Vesktop", "Discord-клиент с Vencord внутри");

        Assert.True(exact > typo);
        Assert.Equal(CatalogSearch.ScoreExact, exact);
    }

    // Real qBittorrent description: Russian text must match via invariant lowercasing
    [Fact]
    public void Score_DescriptionMatch_RealCatalogText()
        => Assert.Equal(CatalogSearch.ScoreDescription,
            CatalogSearch.Score("торрент", "qBittorrent", QbittorrentDesc));

    // Subsequence tier: "vsp" letters appear in order inside "Vesktop"
    [Fact]
    public void Score_Subsequence_FindsOrderedChars()
        => Assert.Equal(CatalogSearch.ScoreSubsequence, CatalogSearch.Score("vsp", "Vesktop", null));

    // Whitespace/case noise must not break matching (UI input is untrusted)
    [Fact]
    public void Score_TrimsAndIgnoresCase()
        => Assert.Equal(CatalogSearch.ScoreExact, CatalogSearch.Score("  YT-DLP ", YtDlpName, YtDlpDesc));

    // Empty/null query scores 0 — "show everything" is the caller's job, not a match
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Score_EmptyQuery_IsZero(string? q)
        => Assert.Equal(0, CatalogSearch.Score(q, YtDlpName, YtDlpDesc));

    // No name and no description match → 0
    [Fact]
    public void Score_UnrelatedQuery_IsZero()
        => Assert.Equal(0, CatalogSearch.Score("photoshop", YtDlpName, YtDlpDesc));
}
