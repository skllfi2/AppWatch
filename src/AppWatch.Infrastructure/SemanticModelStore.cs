using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace AppWatch.Infrastructure;

/// <summary>
/// Downloads and stores the semantic-search model assets (ONNX + HF tokenizer.json)
/// under %APPDATA%\AppWatch\models\semantic\. URLs come from AppConfig (overridable —
/// HF mirrors can be swapped without a rebuild), falling back to defaults.
/// </summary>
public sealed class SemanticModelStore
{
    // paraphrase-multilingual-MiniLM-L12-v2: 384-dim, RU+EN, int8-quantized ONNX (~118 MB)
    public const string DefaultModelUrl =
        "https://huggingface.co/Xenova/paraphrase-multilingual-MiniLM-L12-v2/resolve/main/onnx/model_quantized.onnx";
    public const string DefaultTokenizerUrl =
        "https://huggingface.co/Xenova/paraphrase-multilingual-MiniLM-L12-v2/resolve/main/tokenizer.json";

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(10) };

    private readonly string _dir;
    private readonly AppConfigProxy? _config;

    public SemanticModelStore(string? appDataDir = null, AppConfigProxy? config = null)
    {
        _dir = Path.Combine(
            appDataDir ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AppWatch"),
            "models", "semantic");
        _config = config;
    }

    public string ModelPath => Path.Combine(_dir, "model.onnx");
    public string TokenizerPath => Path.Combine(_dir, "tokenizer.json");

    public string ModelUrl => _config?.EmbeddingModelUrl is { Length: > 0 } u ? u : DefaultModelUrl;
    public string TokenizerUrl => _config?.EmbeddingTokenizerUrl is { Length: > 0 } u ? u : DefaultTokenizerUrl;

    public bool IsInstalled =>
        File.Exists(ModelPath) && new FileInfo(ModelPath).Length > 1_000_000
        && File.Exists(TokenizerPath) && new FileInfo(TokenizerPath).Length > 1_000;

    public long InstalledSizeBytes =>
        IsInstalled ? new FileInfo(ModelPath).Length : 0;

    /// <summary>
    /// Downloads both assets with progress. Temporary .part files + atomic move —
    /// a torn download never looks "installed".
    /// </summary>
    public async Task DownloadAsync(IProgress<double>? progress = null)
    {
        Directory.CreateDirectory(_dir);

        await DownloadFileAsync(ModelUrl, ModelPath, progress);
        await DownloadFileAsync(TokenizerUrl, TokenizerPath, null);
    }

    private static async Task DownloadFileAsync(string url, string target, IProgress<double>? progress)
    {
        using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength ?? -1;
        var tmp = target + ".part";
        await using (var src = await response.Content.ReadAsStreamAsync())
        await using (var dst = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 16))
        {
            var buffer = new byte[1 << 16];
            long read = 0;
            int n;
            while ((n = await src.ReadAsync(buffer)) > 0)
            {
                await dst.WriteAsync(buffer.AsMemory(0, n));
                read += n;
                if (total > 0)
                    progress?.Report((double)read / total * 100);
            }
        }
        File.Move(tmp, target, overwrite: true);
    }
}

/// <summary>Config read-access for Infrastructure (AppConfig lives in Core; this decouples the store).</summary>
public sealed class AppConfigProxy
{
    public string? EmbeddingModelUrl { get; init; }
    public string? EmbeddingTokenizerUrl { get; init; }
}
