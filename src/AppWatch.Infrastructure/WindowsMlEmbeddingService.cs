using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AppWatch.Core.Domain;
using Windows.AI.MachineLearning;

namespace AppWatch.Infrastructure;

/// <summary>
/// Windows ML embedding service: runs an int8-quantized sentence-transformer ONNX
/// (paraphrase-multilingual-MiniLM-L12-v2, 384-dim) on the OS-provided WinML runtime.
/// CPU device on purpose — quantized kernels are guaranteed there; a 384-dim MiniLM
/// embeds a short text in tens of ms on CPU, which is plenty for catalog search.
///
/// Adaptive binding: the model's declared inputs (input_ids / attention_mask /
/// token_type_ids) are discovered at load; missing inputs are skipped. Output is
/// either a pooled [1, 384] vector or a token-level [1, seq, 384] hidden state —
/// the latter gets masked mean-pooled.
/// </summary>
public sealed class WindowsMlEmbeddingService : IEmbeddingService
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    private LearningModel? _model;
    private LearningModelSession? _session;
    private UnigramTokenizer? _tokenizer;
    private HashSet<string> _inputNames = new(StringComparer.Ordinal);
    private string _outputName = "embeddings";
    private bool _outputIsTokenLevel;

    public bool IsReady => _model is not null && _session is not null && _tokenizer is not null;

    public int? VectorDimension { get; private set; }

    public string? LastError { get; private set; }

    public async Task<bool> LoadAsync(string modelPath, string tokenizerPath)
    {
        await _gate.WaitAsync();
        try
        {
            _model = null;
            _session = null;
            _tokenizer = null;
            LastError = null;
            VectorDimension = null;

            if (!File.Exists(modelPath)) { LastError = "model file not found"; return false; }
            if (!File.Exists(tokenizerPath)) { LastError = "tokenizer file not found"; return false; }

            _model = await Task.Run(() => LearningModel.LoadFromFilePath(modelPath));

            // Discover inputs
            _inputNames = _model.InputFeatures
                .Where(f => f.Kind == LearningModelFeatureKind.Tensor)
                .Select(f => f.Name)
                .ToHashSet(StringComparer.Ordinal);

            // Discover output: prefer pooled "embeddings", else the first tensor output
            var outFeature = _model.OutputFeatures
                .FirstOrDefault(f => f.Kind == LearningModelFeatureKind.Tensor && f.Name == "embeddings")
                ?? _model.OutputFeatures.FirstOrDefault(f => f.Kind == LearningModelFeatureKind.Tensor);
            if (outFeature is null) { LastError = "model has no tensor output"; return false; }
            _outputName = outFeature.Name;

            if (outFeature is TensorFeatureDescriptor tensor)
            {
                var shape = tensor.Shape;
                if (shape.Count == 3)
                {
                    _outputIsTokenLevel = true;
                    VectorDimension = (int)shape[2];
                }
                else if (shape.Count == 2)
                {
                    _outputIsTokenLevel = false;
                    VectorDimension = (int)shape[1];
                }
            }

            _session = new LearningModelSession(_model); // default = CPU device

            _tokenizer = UnigramTokenizer.FromHuggingFaceJson(await File.ReadAllTextAsync(tokenizerPath));

            return IsReady;
        }
        catch (Exception ex)
        {
            LastError = $"{ex.GetType().Name}: {ex.Message}";
            _model = null;
            _session = null;
            _tokenizer = null;
            return false;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<float[]?> EmbedAsync(string text)
    {
        if (!IsReady || _session is null || _tokenizer is null || _model is null)
            return null;

        var ids = _tokenizer.Encode(text, maxTokens: 128);
        try
        {
            await _gate.WaitAsync();
            try
            {
                var result = await Task.Run(() =>
                {
                    var binding = new LearningModelBinding(_session);

                    var shape = new long[] { 1, ids.Length };
                    binding.Bind("input_ids", TensorInt64Bit.CreateFromArray(shape, ids.Select(i => (long)i).ToArray()));
                    if (_inputNames.Contains("attention_mask"))
                        binding.Bind("attention_mask", TensorInt64Bit.CreateFromArray(shape, MaskFor(ids.Length)));
                    if (_inputNames.Contains("token_type_ids"))
                        binding.Bind("token_type_ids", TensorInt64Bit.CreateFromArray(shape, new long[ids.Length]));

                    var eval = _session!.Evaluate(binding, "embed");
                    if (eval.Outputs.TryGetValue(_outputName, out var raw) && raw is TensorFloat output)
                        return ReadVector(output, ids.Length);
                    LastError = $"output '{_outputName}' is not a tensor";
                    return null;
                });
                return result;
            }
            finally
            {
                _gate.Release();
            }
        }
        catch (Exception ex)
        {
            LastError = $"{ex.GetType().Name}: {ex.Message}";
            return null;
        }
    }

    private static long[] MaskFor(int n) => Enumerable.Repeat(1L, n).ToArray();

    private float[]? ReadVector(TensorFloat output, int tokenCount)
    {
        var view = output.GetAsVectorView();
        var all = view.ToArray();

        if (!_outputIsTokenLevel)
            return all; // [1, dim]

        // [1, seq, dim] → masked mean pooling (mask = all ones here, so plain mean over seq)
        var dim = VectorDimension ?? (all.Length / tokenCount);
        if (dim == 0) return null;

        var pooled = new float[dim];
        var seq = all.Length / dim;
        for (var t = 0; t < seq; t++)
            for (var d = 0; d < dim; d++)
                pooled[d] += all[t * dim + d];
        for (var d = 0; d < dim; d++)
            pooled[d] /= seq;
        return pooled;
    }
}
