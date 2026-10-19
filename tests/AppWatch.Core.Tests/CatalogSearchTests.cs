using AppWatch.Core.Domain;
using AppWatch.Core.Domain.Models;
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

    // === E6: Rank over the REAL 16-entry catalog. Names+descriptions baked inline from
    // src/AppWatch.App/Catalog/catalog.json (read-only allowed source) — hermetic. ===

    private static IReadOnlyList<CatalogEntry> RealCatalog() =>
    [
        new() { Name = "zapret-discord-youtube", RepoOwner = "Flowseal", RepoName = "zapret-discord-youtube", Description = "Обход замедления Discord и YouTube (Flowseal)" },
        new() { Name = "zapret2-next", RepoOwner = "Dunterbabochka", RepoName = "zapret2-next", Description = "Zapret 2 для Windows с bat-лаунчерами (Dunterbabochka)" },
        new() { Name = "yt-dlp", RepoOwner = "yt-dlp", RepoName = "yt-dlp", Description = "Загрузчик YouTube: видео, аудио, плейлисты (обновляется еженедельно)" },
        new() { Name = "Vesktop", RepoOwner = "Vencord", RepoName = "Vesktop", Description = "Discord-клиент с Vencord внутри (когда Discord душат — особенно актуален)" },
        new() { Name = "AmneziaVPN", RepoOwner = "amnezia-vpn", RepoName = "amnezia-client", Description = "Self-hosted VPN-клиент с обфускацией AmneziaWG" },
        new() { Name = "qBittorrent", RepoOwner = "qbittorrent", RepoName = "qBittorrent", Description = "Торрент-клиент без рекламы (стандарт де-факто)" },
        new() { Name = "optimizerDuck", RepoOwner = "itsfatduck", RepoName = "optimizerDuck", Description = "Бесплатный оптимизатор Windows: производительность и приватность" },
        new() { Name = "GameOptimizer", RepoOwner = "charlie754", RepoName = "Game-Optimizer-CPUs-Threads-Optimizer", Description = "Оптимизатор игр: привязка к CPU-кластерам через CPU Sets API" },
        new() { Name = "Rufus", RepoOwner = "pbatard", RepoName = "rufus", Description = "Создание загрузочных USB (1.9 МБ, без установки)" },
        new() { Name = "Ventoy", RepoOwner = "ventoy", RepoName = "Ventoy", Description = "Мультизагрузочные USB: копируй ISO прямо на флешку" },
        new() { Name = "ShareX", RepoOwner = "ShareX", RepoName = "ShareX", Description = "Скриншоты, записи экрана, загрузки — мощнейший инструмент" },
        new() { Name = "OBS Studio", RepoOwner = "obsproject", RepoName = "obs-studio", Description = "Запись и стриминг (портативная сборка x64)" },
        new() { Name = "RustDesk", RepoOwner = "rustdesk", RepoName = "rustdesk", Description = "Удалённый доступ (open-source TeamViewer, самохостинг)" },
        new() { Name = "VSCodium", RepoOwner = "VSCodium", RepoName = "vscodium", Description = "VS Code без телеметрии Microsoft (обновляется ежедневно)" },
        new() { Name = "PowerToys", RepoOwner = "microsoft", RepoName = "PowerToys", Description = "Набор утилит Microsoft для продуктивности и кастомизации" },
        new() { Name = "GPU-Z", RepoOwner = "TechPowerUp", RepoName = "GPU-Z", Description = "Информация о видеокарте и датчики (TechPowerUp)" },
    ];

    // RESEARCH.md D3 case 1 at list level: "ytdl" ranks yt-dlp first over 15 others
    [Fact]
    public void Rank_Ytdl_ReturnsYtDlpFirst()
    {
        var hits = CatalogSearch.Rank(RealCatalog(), "ytdl");

        Assert.NotEmpty(hits);
        Assert.Equal("yt-dlp", hits[0].Name);
    }

    // RESEARCH.md D3 case 2 at list level: "vescop" typo ranks Vesktop first
    [Fact]
    public void Rank_VescopTypo_ReturnsVesktopFirst()
    {
        var hits = CatalogSearch.Rank(RealCatalog(), "vescop");

        Assert.NotEmpty(hits);
        Assert.Equal("Vesktop", hits[0].Name);
    }

    // RESEARCH.md D3 case 3 at list level: "optimizer" returns exactly the two
    // optimizer entries, duck first (prefix 90 > substring 80)
    [Fact]
    public void Rank_Optimizer_ReturnsExactlyBothOptimizers()
    {
        var hits = CatalogSearch.Rank(RealCatalog(), "optimizer");

        Assert.Equal(["optimizerDuck", "GameOptimizer"], hits.Select(h => h.Name).ToArray());
    }

    // Real catalog: BOTH zapret names start with "zapret" (prefix tier tie, 90) →
    // deterministic tie-break by name ordinal: '-' (45) < '2' (50)
    [Fact]
    public void Rank_TiedPrefixScores_BreakByNameOrdinal()
    {
        var hits = CatalogSearch.Rank(RealCatalog(), "zapret");

        Assert.Equal(["zapret-discord-youtube", "zapret2-next"], hits.Select(h => h.Name).ToArray());
    }

    // Real catalog tier order: "youtube" — zapret-discord-youtube matches the NAME
    // (substring, 80) and must rank above yt-dlp which only matches its DESCRIPTION (40)
    [Fact]
    public void Rank_NameMatch_BeatsDescriptionMatch()
    {
        var hits = CatalogSearch.Rank(RealCatalog(), "youtube");

        Assert.Equal(["zapret-discord-youtube", "yt-dlp"], hits.Select(h => h.Name).ToArray());
    }

    // Real catalog: "usb" matches only two DESCRIPTIONS at the same tier (40) →
    // deterministic tie-break by name ordinal: Rufus < Ventoy
    [Fact]
    public void Rank_TiedDescriptionScores_BreakByNameOrdinal()
    {
        var hits = CatalogSearch.Rank(RealCatalog(), "usb");

        Assert.Equal(["Rufus", "Ventoy"], hits.Select(h => h.Name).ToArray());
    }

    // Empty/null query → no forced matches; showing everything is the caller's job
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Rank_EmptyQuery_ReturnsNothing(string? q)
        => Assert.Empty(CatalogSearch.Rank(RealCatalog(), q));
}
