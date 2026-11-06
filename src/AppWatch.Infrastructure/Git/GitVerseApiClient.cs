using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using AppWatch.Core.Domain;
using AppWatch.Core.Domain.Models;

namespace AppWatch.Infrastructure.Git;

/// <summary>
/// GitVerse API client (compatible with Gitea API v1 standard).
/// Supports fetching releases and downloading assets from gitverse.ru.
/// </summary>
public sealed class GitVerseApiClient : IGitReleaseClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _http;
    private readonly string _baseUrl;

    public string ProviderName => "GitVerse";

    public GitVerseApiClient(HttpClient? httpClient = null, string baseUrl = "https://gitverse.ru/api/v1/")
    {
        _baseUrl = baseUrl.TrimEnd('/') + "/";
        _http = httpClient ?? new HttpClient();
    }

    public bool CanHandle(string? repositoryUrlOrHost)
    {
        if (string.IsNullOrWhiteSpace(repositoryUrlOrHost)) return false;
        return repositoryUrlOrHost.Contains("gitverse.ru", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<IReadOnlyList<ReleaseInfo>> GetReleasesAsync(
        string owner,
        string repo,
        CancellationToken cancellationToken = default,
        string? etag = null)
    {
        var url = $"{_baseUrl}repos/{owner}/{repo}/releases";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);

        if (!string.IsNullOrEmpty(etag))
        {
            request.Headers.IfNoneMatch.Add(new EntityTagHeaderValue(etag));
        }

        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotModified)
        {
            return []; // Up to date via ETag
        }

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var dtos = JsonSerializer.Deserialize<List<GiteaReleaseDto>>(json, JsonOptions) ?? [];

        return dtos.Select(MapDto).ToList();
    }

    public async Task DownloadAssetAsync(
        string downloadUrl,
        string destinationPath,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        using var response = await _http.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength ?? -1L;
        var dir = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        var tempPath = destinationPath + ".part";
        await using (var src = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
        await using (var dst = File.Create(tempPath))
        {
            var buffer = new byte[81920];
            long written = 0;
            int read;
            while ((read = await src.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                await dst.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                written += read;
                if (total > 0) progress?.Report(written * 100.0 / total);
            }
        }

        File.Move(tempPath, destinationPath, overwrite: true);
    }

    public static ReleaseInfo ParseGiteaRelease(string json)
    {
        var dto = JsonSerializer.Deserialize<GiteaReleaseDto>(json, JsonOptions)
                  ?? throw new JsonException("Failed to deserialize Gitea release");
        return MapDto(dto);
    }

    private static ReleaseInfo MapDto(GiteaReleaseDto dto)
    {
        var assets = (dto.Assets ?? []).Select(a => new ReleaseAsset(
            Name: a.Name ?? "unnamed",
            DownloadUrl: a.BrowserDownloadUrl ?? "",
            Size: a.Size,
            DigestSha256: null
        )).ToList();

        return new ReleaseInfo
        {
            TagName = dto.TagName ?? "",
            IsPreRelease = dto.Prerelease,
            PublishedAt = dto.PublishedAt,
            Assets = assets,
            Body = dto.Body
        };
    }

    private sealed class GiteaReleaseDto
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("prerelease")]
        public bool Prerelease { get; set; }

        [JsonPropertyName("published_at")]
        public DateTimeOffset PublishedAt { get; set; }

        [JsonPropertyName("body")]
        public string? Body { get; set; }

        [JsonPropertyName("assets")]
        public List<GiteaAssetDto>? Assets { get; set; }
    }

    private sealed class GiteaAssetDto
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("browser_download_url")]
        public string? BrowserDownloadUrl { get; set; }

        [JsonPropertyName("size")]
        public long Size { get; set; }
    }
}
