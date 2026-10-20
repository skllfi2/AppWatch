using System;
using System.Threading.Tasks;

namespace AppWatch.Infrastructure;

/// <summary>
/// On-device text embeddings (semantic catalog search). Implemented by
/// <see cref="WindowsMlEmbeddingService"/> via the OS-provided Windows ML
/// runtime (Windows.AI.MachineLearning) — no native packages shipped.
/// </summary>
public interface IEmbeddingService
{
    /// <summary>True after a successful <see cref="LoadAsync"/> (model + tokenizer ready).</summary>
    bool IsReady { get; }

    /// <summary>Dimension of the embedding vectors (384 for MiniLM-L12-H384).</summary>
    int? VectorDimension { get; }

    /// <summary>Last load/init failure message (for the Settings diagnostics row).</summary>
    string? LastError { get; }

    /// <summary>Loads the ONNX model + HF tokenizer.json. Returns false on any failure (see LastError).</summary>
    Task<bool> LoadAsync(string modelPath, string tokenizerPath);

    /// <summary>Embeds text, or null when not ready / inference failed.</summary>
    Task<float[]?> EmbedAsync(string text);
}
