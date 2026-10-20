using AppWatch.Core.Domain.Models;

namespace AppWatch.Core.Domain;

/// <summary>
/// Semantic catalog search (P-Sem): merges lexical tiers (<see cref="CatalogSearch"/>) with
/// embedding cosine similarity. Pure math + ranking — the embedding provider is injected by
/// the caller, so this stays hermetic and testable with synthetic vectors.
///
/// Scoring contract:
/// - lexical hit:   score = lexical + (int)(cosine * BoostWeight)      — semantic reorders WITHIN a tier
/// - lexical miss:  score = RescueBase + (int)(cosine * 4)             — only when cosine ≥ RescueThreshold;
///                                                                        rescued entries rank just below the
///                                                                        description tier (40), never above it
/// - no vectors for an entry / query → pure lexical behaviour (feature degrades to status quo).
/// </summary>
public static class SemanticSearch
{
    /// <summary>Cosine below this never rescues a lexically-missed entry.</summary>
    public const double RescueThreshold = 0.35;

    /// <summary>Base score for a semantic-rescued entry: 35..39, i.e. under the description tier (40).</summary>
    public const int RescueBase = 35;

    /// <summary>How strongly cosine reorders entries that already matched lexically.</summary>
    public const int BoostWeight = 15;

    /// <summary>Cosine similarity; 0 for null/mismatched-length vectors. Pure math, no allocations beyond the dot loop.</summary>
    public static double CosineSimilarity(float[]? a, float[]? b)
    {
        if (a is null || b is null || a.Length == 0 || a.Length != b.Length) return 0;

        double dot = 0, na = 0, nb = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += (double)a[i] * b[i];
            na += (double)a[i] * a[i];
            nb += (double)b[i] * b[i];
        }
        if (na == 0 || nb == 0) return 0;
        return dot / (Math.Sqrt(na) * Math.Sqrt(nb));
    }

    /// <summary>
    /// Rank with semantic merge: lexical tiers first (boosted by cosine), then semantic rescues.
    /// Entry vectors ride along via <paramref name="vectorOf"/> (the caller owns the index).
    /// Deterministic: score desc, then name asc (ordinal, case-insensitive) — the CatalogSearch.Rank contract.
    /// </summary>
    public static IReadOnlyList<T> Rank<T>(
        IReadOnlyList<T> entries, string? query,
        Func<T, string> name, Func<T, string?> description,
        Func<T, float[]?> vectorOf,
        float[]? queryVector)
    {
        if (string.IsNullOrWhiteSpace(query)) return [];
        var q = query.Trim();

        return entries
            .Select(e =>
            {
                var lexical = CatalogSearch.Score(q, name(e), description(e));
                if (queryVector is null) return (Entry: e, Score: lexical);

                var cos = CosineSimilarity(queryVector, vectorOf(e));
                var score = lexical > 0
                    ? lexical + (int)(Math.Clamp(cos, 0, 1) * BoostWeight)
                    : cos >= RescueThreshold ? RescueBase + (int)(cos * 4) : 0;
                return (Entry: e, Score: score);
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => name(x.Entry), StringComparer.OrdinalIgnoreCase)
            .Select(x => x.Entry)
            .ToList();
    }
}
