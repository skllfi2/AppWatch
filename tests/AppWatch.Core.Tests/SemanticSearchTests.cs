using AppWatch.Core.Domain;
using AppWatch.Core.Domain.Models;
using Xunit;

namespace AppWatch.Core.Tests;

/// <summary>
/// P-Sem: semantic merge over lexical tiers. Vectors are synthetic (2-D floats) —
/// the contract is the SCORING MATH, not the embedding model.
/// </summary>
public class SemanticSearchTests
{
    private static readonly float[] Up = [0f, 1f];
    private static readonly float[] Right = [1f, 0f];
    private static readonly float[] Diag = [1f, 1f];

    // Cosine basics: orthogonal = 0, identical direction = 1, scaled = 1, mismatch = 0
    [Fact]
    public void Cosine_OrthogonalIsZero_SameIsOne()
    {
        Assert.Equal(0, SemanticSearch.CosineSimilarity(Up, Right), 6);
        Assert.Equal(1, SemanticSearch.CosineSimilarity(Up, [0f, 2f]), 6);
        Assert.Equal(1, SemanticSearch.CosineSimilarity(Diag, [2f, 2f]), 6);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(new float[0])]
    public void Cosine_DegenerateVectors_AreZero(float[]? v)
        => Assert.Equal(0, SemanticSearch.CosineSimilarity(v, Up));

    [Fact]
    public void Cosine_LengthMismatch_IsZero()
        => Assert.Equal(0, SemanticSearch.CosineSimilarity([1f, 2f], [1f, 2f, 3f]));

    // === Rank: rescue + boost + degradation ===

    private sealed record Entry(string Name, string? Description, float[]? Vector);

    private static IReadOnlyList<Entry> Catalog() =>
    [
        new("KeePassXC", "Менеджер паролей", Up),
        new("Joplin", "Заметки", Right),
        new("Notepad++", "Продвинутый текстовый редактор", Right),
    ];

    [Fact]
    public void Rank_WithoutQueryVector_FallsBackToPureLexical()
    {
        var hits = SemanticSearch.Rank(Catalog(), "notepad",
            e => e.Name, e => e.Description, _ => (float[]?)null, null);

        Assert.Equal(["Notepad++"], hits.Select(h => h.Name).ToArray());
    }

    [Fact]
    public void Rank_ExactLexicalHit_StillBeatsSemanticRescue()
    {
        // query "notepad" lexically exact-hits Notepad++; KeePassXC sits at cos 1.0 with
        // the query vector but only via rescue (35..39 < 100); Joplin is orthogonal (dropped)
        var hits = SemanticSearch.Rank(Catalog(), "notepad",
            e => e.Name, e => e.Description, e => e.Vector, Up);

        Assert.Equal(["Notepad++", "KeePassXC"], hits.Select(h => h.Name).ToArray());
    }

    [Fact]
    public void Rank_SemanticRescue_FindsLexicallyMissedEntry()
    {
        // "password manager" has no lexical overlap with the Russian description —
        // only the vector rescues KeePassXC
        var hits = SemanticSearch.Rank(Catalog(), "password manager",
            e => e.Name, e => e.Description, e => e.Vector, Up);

        Assert.Contains(hits, h => h.Name == "KeePassXC");
        Assert.Equal("KeePassXC", hits[0].Name); // nothing else matched at all
    }

    [Fact]
    public void Rank_RescueBelowThreshold_DropsEntry()
    {
        // query vector [1, 0.1]: cos vs KeePassXC(Up) ≈ 0.10 < 0.35 → dropped;
        // cos vs Joplin(Right) ≈ 0.995 ≥ 0.35 → rescued
        var hits = SemanticSearch.Rank(Catalog(), "password manager",
            e => e.Name, e => e.Description, e => e.Vector, [1f, 0.1f]);

        Assert.Contains(hits, h => h.Name == "Joplin");
        Assert.DoesNotContain(hits, h => h.Name == "KeePassXC");
    }

    [Fact]
    public void Rank_BoostReordersWithinTier()
    {
        // both entries hit the description tier (40); the vector decides the order
        var entries = new List<Entry>
        {
            new("Alpha", "заметки", Right),          // cos 0 → 40
            new("Beta", "заметки и todo", Up),       // cos 1 → 40 + 15
        };
        var hits = SemanticSearch.Rank(entries, "заметки",
            e => e.Name, e => e.Description, e => e.Vector, Up);

        Assert.Equal(["Beta", "Alpha"], hits.Select(h => h.Name).ToArray());
    }

    [Fact]
    public void Rank_NoVectorForEntry_IsPureLexicalForThatEntry()
    {
        var entries = new List<Entry>
        {
            new("Alpha", "текст", Up),
            new("Beta", "текст", null), // vector missing
        };
        var hits = SemanticSearch.Rank(entries, "текст",
            e => e.Name, e => e.Description, e => e.Vector, Up);

        // Alpha: 40 + boost(15); Beta: 40 + boost(0) — deterministic order
        Assert.Equal(["Alpha", "Beta"], hits.Select(h => h.Name).ToArray());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Rank_EmptyQuery_ReturnsNothing(string? q)
        => Assert.Empty(SemanticSearch.Rank(Catalog(), q, e => e.Name, e => e.Description, e => e.Vector, Up));

    [Fact]
    public void Rank_Deterministic_NameTieBreak()
    {
        var entries = new List<Entry>
        {
            new("Zulu", "abc", Up),
            new("Yankee", "abc", Up),
        };
        var hits = SemanticSearch.Rank(entries, "abc",
            e => e.Name, e => e.Description, e => e.Vector, Up);

        Assert.Equal(["Yankee", "Zulu"], hits.Select(h => h.Name).ToArray());
    }
}

/// <summary>
/// Unigram tokenizer: Viterbi segmentation, Metaspace, normalization, specials,
/// truncation — all against tiny synthetic vocabularies (hermetic).
/// </summary>
public class UnigramTokenizerTests
{
    private const int Unk = 3, Cls = 0, Sep = 2;

    private static UnigramTokenizer Make(bool lowercase = true) => UnigramTokenizer.FromPairs(
    [
        ("▁пароль", 10, -1f),
        ("▁за", 11, -2f),
        ("мет", 12, -3f),
        ("ы", 13, -4f),
        ("▁abc", 14, -2f),
        ("▁ab", 15, -6f),
        ("c", 16, -6f),
        ("▁x", 17, -2f),
    ], unkId: Unk, clsId: Cls, sepId: Sep, lowercase: lowercase, nfkc: false);

    [Fact]
    public void Encode_WrapsWithSpecials_OnEmpty()
        => Assert.Equal([Cls, Sep], Make().Encode(""));

    [Fact]
    public void Encode_MetaspacePrefix_SegmentsWord()
    {
        // "пароль" → "▁пароль" (single piece) + [CLS]/[SEP]
        Assert.Equal([Cls, 10, Sep], Make().Encode("пароль"));
    }

    [Fact]
    public void Encode_ViterbiPrefersHighScoringPiece()
    {
        // "abc": "▁abc" (-2) beats "▁ab"(-6) + "c"(-6)
        Assert.Equal([Cls, 14, Sep], Make().Encode("abc"));
    }

    [Fact]
    public void Encode_SplitsIntoBestPieces()
    {
        // "замет" has no single piece: "▁за" + "мет" (‑2 + ‑3) — no full-word piece exists
        Assert.Equal([Cls, 11, 12, Sep], Make().Encode("замет"));
    }

    [Fact]
    public void Encode_UnknownChar_GoesToUnk()
    {
        // "qq" is not in vocab at all; metaspace prefix "▁" is not a piece either
        // → unk per char: ▁ q q
        Assert.Equal([Cls, Unk, Unk, Unk, Sep], Make().Encode("qq"));
    }

    [Fact]
    public void Encode_AppliesLowercase()
    {
        var tok = Make(lowercase: true);
        // "ABC" lowercased → "abc" → piece 14
        Assert.Equal([Cls, 14, Sep], tok.Encode("ABC"));
    }

    [Fact]
    public void Encode_RespectsCaseWhenDisabled()
    {
        var tok = Make(lowercase: false);
        // "ABC" stays uppercase → "▁ABC", none of it in vocab → 4 unks (incl. the ▁ char)
        Assert.Equal([Cls, Unk, Unk, Unk, Unk, Sep], tok.Encode("ABC"));
    }

    [Fact]
    public void Encode_TruncatesToMaxTokens()
    {
        var tok = Make();
        var ids = tok.Encode("пароль пароль пароль пароль пароль пароль пароль пароль пароль пароль", maxTokens: 8);
        // 8 = [CLS] + 6 content pieces max + [SEP]
        Assert.Equal(8, ids.Length);
        Assert.Equal(Cls, ids[0]);
        Assert.Equal(Sep, ids[^1]);
    }

    [Fact]
    public void FromHuggingFaceJson_ParsesStructure()
    {
        const string json = """
        {
          "normalizer": { "type": "Sequence", "normalizers": [ { "type": "NFKC" }, { "type": "Lowercase" } ] },
          "pre_tokenizer": { "type": "Metaspace", "replacement": "▁", "add_prefix_space": true },
          "model": { "type": "Unigram", "unk_id": 3, "vocab": [ ["<pad>", -5.0], ["▁hi", -1.0], ["▁h", -6.0], ["i", -6.0] ] },
          "added_tokens": [ { "id": 0, "content": "<s>" }, { "id": 2, "content": "</s>" }, { "id": 3, "content": "<unk>" } ]
        }
        """;
        var tok = UnigramTokenizer.FromHuggingFaceJson(json);
        Assert.Equal([Cls, 1, Sep], tok.Encode("HI"));
    }
}
