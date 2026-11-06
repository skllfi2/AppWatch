using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using AppWatch.Core.Domain;
using AppWatch.Core.Domain.Models;

namespace AppWatch.Infrastructure.Git;

/// <summary>
/// GitLab API client (compatible with GitLab REST API v4).
/// Supports fetching releases and downloading release link assets from gitlab.com and self-hosted instances.
/// </summary>
public sealed class GitLabApiClient : IGitReleaseClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _http;
    private readonly string _baseUrl;

    public string ProviderName => "GitLab";

    public GitLabApiClient(HttpClient? httpClient = null, string baseUrl = "https://gitlab.com/api/v4/")
    {
        _baseUrl = baseUrl.TrimEnd('/') + "/";
        _http = httpClient ?? new HttpClient();
    }

    public bool CanHandle(string? repositoryUrlOrHost)
    {
        if (string.IsNullOrWhiteSpace(repositoryUrlOrHost)) return false;
        return repositoryUrlOrHost.Contains("gitlab.com", StringComparison.OrdinalIgnoreCase) ||
               repositoryUrlOrHost.Contains("gitlab", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<IReadOnlyList<ReleaseInfo>> GetReleasesAsync(
        string owner,
        string repo,
        CancellationToken cancellationToken = default,
        string? etag = null)
    {
        var projectPath = Uri.EscapeDataString($"{owner}/{repo}");
        var url = $"{_baseUrl}projects/{projectPath}/releases";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);

        if (!string.IsNullOrEmpty(etag))
        {
            request.Headers.IfNoneMatch.Add(new EntityTagHeaderValue(etag));
        }

        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotModified)
        {
            return [];
        }

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var dtos = JsonSerializer.Deserialize<List<GitLabReleaseDto>>(json, JsonOptions) ?? [];

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

    public static ReleaseInfo ParseGitLabRelease(string json)
    {
        var dto = JsonSerializer.Deserialize<GitLabReleaseDto>(json, JsonOptions)
                  ?? throw new JsonException("Failed to deserialize GitLab release");
        return MapDto(dto);
    }

    private static ReleaseInfo MapDto(GitLabReleaseDto dto)
    {
        var assets = (dto.Assets?.Links ?? []).Select(link => new ReleaseAsset(
            Name: link.Name ?? "unnamed",
            DownloadUrl: link.Url ?? "",
            Size: 0,
            DigestSha256: null
        )).ToList();

        var isPre = !string.IsNullOrEmpty(dto.TagName) &&
                    (dto.TagName.Contains("alpha", StringComparison.OrdinalIgnoreCase) ||
                     dto.TagName.Contains("beta", StringComparison.OrdinalIgnoreCase) ||
                     dto.TagName.Contains("rc", StringComparison.OrdinalIgnoreCase));

        return new ReleaseInfo
        {
            TagName = dto.TagName ?? "",
            IsPreRelease = isPre,
            PublishedAt = dto.ReleasedAt,
            Assets = assets,
            Body = dto.Description
        };
    }

    private sealed class GitLabReleaseDto
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("released_at")]
        public DateTimeOffset ReleasedAt { get; set; }

        [JsonPropertyName("assets")]
        public GitLabAssetsContainerDto? Assets { get; set; }
    }

    private sealed class GitLabAssetsContainerDto
    {
        [JsonPropertyName("links")]
        public List<GitLabAssetLinkDto>? Links { get; set; }
    }

    private sealed class GitLabAssetLinkDto
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("url")]
        public string? Url { get; set; }
    }
}
