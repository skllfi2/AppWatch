using System.Text.Json;

namespace AppWatch.Core.Domain;

/// <summary>
/// SentencePiece-Unigram tokenizer (XLM-R family) in pure C# — no native deps, hermetic.
/// Parses the Hugging Face tokenizer.json (vocab pieces + scores, normalizer, Metaspace
/// pre-tokenizer, special ids) and encodes text to model input ids via Viterbi
/// segmentation, with [CLS]…[SEP] wrapping and truncation.
///
/// Sentence-transformer models used by the catalog semantic search
/// (paraphrase-multilingual-MiniLM-L12-v2) use exactly this tokenizer family.
/// </summary>
public sealed class UnigramTokenizer
{
    private readonly Dictionary<string, (int Id, float Score)> _pieces;
    private readonly int _unkId;
    private readonly int _clsId;
    private readonly int _sepId;
    private readonly bool _lowercase;
    private readonly bool _nfkc;
    private readonly string _spaceReplacement;
    private readonly bool _addPrefixSpace;
    private readonly int _maxPieceLen;
    private readonly float _unkPenalty;

    private UnigramTokenizer(
        Dictionary<string, (int Id, float Score)> pieces, int unkId, int clsId, int sepId,
        bool lowercase, bool nfkc, string spaceReplacement, bool addPrefixSpace)
    {
        _pieces = pieces;
        _unkId = unkId;
        _clsId = clsId;
        _sepId = sepId;
        _lowercase = lowercase;
        _nfkc = nfkc;
        _spaceReplacement = spaceReplacement;
        _addPrefixSpace = addPrefixSpace;
        _maxPieceLen = pieces.Keys.Count > 0 ? Math.Min(32, pieces.Keys.Max(k => k.Length)) : 1;
        _unkPenalty = pieces.Count > 0 ? pieces.Values.Min(p => p.Score) - 10f : -10f;
    }

    /// <summary>Builds a tokenizer from raw (piece, id, score) pairs — test/repair entry point.</summary>
    public static UnigramTokenizer FromPairs(
        IEnumerable<(string Piece, int Id, float Score)> pairs, int unkId, int clsId, int sepId,
        bool lowercase = false, bool nfkc = true, string spaceReplacement = "▁", bool addPrefixSpace = true)
    {
        var map = new Dictionary<string, (int, float)>();
        foreach (var (piece, id, score) in pairs)
            map[piece] = (id, score);
        return new UnigramTokenizer(map, unkId, clsId, sepId, lowercase, nfkc, spaceReplacement, addPrefixSpace);
    }

    /// <summary>
    /// Parses a Hugging Face tokenizer.json: { normalizer, pre_tokenizer: Metaspace,
    /// model: { type: "Unigram", unk_id, vocab: [[piece, score], …] }, added_tokens: […] }.
    /// Special ids come from added_tokens (looking for the BERT-ish cls/sep names),
    /// falling back to the first three added tokens.
    /// </summary>
    public static UnigramTokenizer FromHuggingFaceJson(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var lowercase = false;
        var nfkc = false;
        if (root.TryGetProperty("normalizer", out var norm) && norm.ValueKind == JsonValueKind.Object)
            ApplyNormalizer(norm, ref lowercase, ref nfkc);

        var replacement = "▁";
        var addPrefix = true;
        if (root.TryGetProperty("pre_tokenizer", out var pre) && pre.ValueKind == JsonValueKind.Object
            && pre.TryGetProperty("type", out var pt) && pt.GetString() == "Metaspace")
        {
            if (pre.TryGetProperty("replacement", out var rep) && rep.ValueKind == JsonValueKind.String)
                replacement = rep.GetString() ?? "▁";
            if (pre.TryGetProperty("add_prefix_space", out var aps) && aps.ValueKind == JsonValueKind.True)
                addPrefix = true;
            else if (pre.TryGetProperty("add_prefix_space", out var aps2) && aps2.ValueKind == JsonValueKind.False)
                addPrefix = false;
        }

        var model = root.GetProperty("model");
        var unkId = model.TryGetProperty("unk_id", out var uid) ? uid.GetInt32() : 0;
        var vocab = model.GetProperty("vocab");
        var pairs = new List<(string, int, float)>(vocab.GetArrayLength());
        var i = 0;
        foreach (var item in vocab.EnumerateArray())
        {
            var piece = item[0].GetString() ?? "";
            var score = item[1].ValueKind == JsonValueKind.Number ? (float)item[1].GetDouble() : 0f;
            pairs.Add((piece, i++, score));
        }

        var clsId = 0;
        var sepId = 2;
        if (root.TryGetProperty("added_tokens", out var added) && added.ValueKind == JsonValueKind.Array)
        {
            foreach (var t in added.EnumerateArray())
            {
                var content = t.GetProperty("content").GetString();
                var id = t.GetProperty("id").GetInt32();
                if (content is "<s>" or "[CLS]") clsId = id;
                else if (content is "</s>" or "[SEP]") sepId = id;
                else if (content is "<unk>" or "[UNK]") unkId = id;
            }
        }

        return FromPairs(pairs, unkId, clsId, sepId, lowercase, nfkc, replacement, addPrefix);
    }

    private static void ApplyNormalizer(JsonElement norm, ref bool lowercase, ref bool nfkc)
    {
        if (!norm.TryGetProperty("type", out var type)) return;
        switch (type.GetString())
        {
            case "Lowercase": lowercase = true; break;
            case "NFKC": nfkc = true; break;
            case "Sequence" when norm.TryGetProperty("normalizers", out var seq):
                foreach (var n in seq.EnumerateArray())
                    ApplyNormalizer(n, ref lowercase, ref nfkc);
                break;
            // "Precompiled" (XLM-R legacy NMT normalization) approximates to NFKC —
            // embeddings are robust to the difference.
            case "Precompiled": nfkc = true; break;
        }
    }

    /// <summary>Encodes text to ids: normalize → whitespace split → Metaspace → Viterbi → [CLS] … [SEP].</summary>
    public int[] Encode(string? text, int maxTokens = 128)
    {
        if (string.IsNullOrWhiteSpace(text)) return [_clsId, _sepId];

        var s = text;
        s = new string(s.Where(c => !char.IsControl(c)).ToArray());
        if (_nfkc) s = s.Normalize(System.Text.NormalizationForm.FormKC);
        if (_lowercase) s = s.ToLowerInvariant();

        var ids = new List<int> { _clsId };
        foreach (var word in s.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            var w = _addPrefixSpace ? _spaceReplacement + word : word;
            EncodeWord(w, ids);
            if (ids.Count >= maxTokens - 1) break; // reserve [SEP]
        }

        if (ids.Count > maxTokens - 1)
            ids.RemoveRange(maxTokens - 1, ids.Count - (maxTokens - 1));
        ids.Add(_sepId);
        return ids.ToArray();
    }

    private void EncodeWord(string word, List<int> ids)
    {
        var n = word.Length;
        var best = new double[n + 1];
        var from = new int[n + 1]; // piece length; -1 = unk char
        for (var i = 1; i <= n; i++) best[i] = double.NegativeInfinity;

        for (var i = 0; i < n; i++)
        {
            if (double.IsNegativeInfinity(best[i])) continue;

            var maxLen = Math.Min(_maxPieceLen, n - i);
            for (var l = 1; l <= maxLen; l++)
            {
                if (!_pieces.TryGetValue(word.Substring(i, l), out var piece)) continue;
                var score = best[i] + piece.Score;
                if (score > best[i + l])
                {
                    best[i + l] = score;
                    from[i + l] = l;
                }
            }

            var unkScore = best[i] + _unkPenalty;
            if (unkScore > best[i + 1])
            {
                best[i + 1] = unkScore;
                from[i + 1] = -1;
            }
        }

        // Walk back from n; on gaps (inf) step by char (unk fallback)
        var pos = n;
        var tokens = new List<int>();
        while (pos > 0)
        {
            int len = from[pos];
            if (len <= 0)
            {
                len = 1; // unk char (either from[pos] == -1 or an unreachable gap)
            }
            var piece = word.Substring(pos - len, len);
            tokens.Add(_pieces.TryGetValue(piece, out var p) ? p.Id : _unkId);
            pos -= len;
        }
        tokens.Reverse();
        ids.AddRange(tokens);
    }
}
