using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using AppWatch.Core.Abstractions;
using AppWatch.Core.Domain.Models;
using Microsoft.Extensions.Caching.Memory;
using Serilog;

namespace AppWatch.Infrastructure.GitHub;

// Retry/resume/digest handling is AppWatch's own; rate-limit tracking and response
// caching are ported from ZuiV2 GitHubNetworkClient (our own code).

public sealed class GitHubApiClient : IGitHubClient
{
    private const int MaxAttempts = 3;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(15);

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly HttpClient _http;
    private readonly MemoryCache _cache = new(new MemoryCacheOptions { SizeLimit = 100 });
    private DateTimeOffset _rateLimitReset = DateTimeOffset.MinValue;
    private int? _remainingRequests;

    public bool IsRateLimited => DateTimeOffset.UtcNow < _rateLimitReset;
    public DateTimeOffset? RateLimitResetUtc => _rateLimitReset == DateTimeOffset.MinValue ? null : _rateLimitReset;
    public int? RemainingRequests => _remainingRequests;

    public GitHubApiClient(string? token = null)
    {
        _http = new HttpClient();
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("AppWatch/0.1");
        _http.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        if (!string.IsNullOrWhiteSpace(token))
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    public async Task<IReadOnlyList<ReleaseInfo>> GetReleasesAsync(
        string owner, string repo, CancellationToken cancellationToken = default,
        bool skipCache = false)
    {
        var url = $"repos/{owner}/{repo}/releases?per_page=30";
        if (!skipCache && _cache.TryGetValue<IReadOnlyList<ReleaseInfo>>(url, out var cached) && cached is not null)
            return cached;

        if (IsRateLimited)
            throw new HttpRequestException($"GitHub API rate limit exceeded until {RateLimitResetUtc:HH:mm:ss}");

        var json = await GetStringWithRetryAsync(url, cancellationToken);
        var dtos = JsonSerializer.Deserialize<List<ReleaseDto>>(json, JsonOptions) ?? [];
        var releases = dtos.Select(Map).ToList();

        _cache.Set(url, releases, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = CacheDuration,
            Size = 1,
        });
        return releases;
    }

    public async Task DownloadFileAsync(
        string url, string destinationPath,
        IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        var partPath = destinationPath + ".part";
        var directory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                var existing = File.Exists(partPath) ? new FileInfo(partPath).Length : 0;
                if (existing > 0)
                    request.Headers.Range = new RangeHeaderValue(existing, null);

                using var response = await _http.SendAsync(
                    request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                response.EnsureSuccessStatusCode();

                // a server that ignores Range answers 200 with the full body — restart from scratch
                var append = existing > 0 && response.StatusCode == HttpStatusCode.PartialContent;
                long baseOffset = append ? existing : 0;
                if (existing > 0 && !append) File.Delete(partPath);

                var total = (response.Content.Headers.ContentLength ?? 0) + baseOffset;
                await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
                await using var target = new FileStream(
                    partPath, append ? FileMode.Append : FileMode.Create, FileAccess.Write);

                var buffer = new byte[81920];
                long written = baseOffset;
                int read;
                while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    written += read;
                    if (total > 0) progress?.Report(written * 100.0 / total);
                }

                File.Move(partPath, destinationPath, overwrite: true);
                return;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (HttpRequestException ex) when (attempt < MaxAttempts && ex.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable)
            {
                if (File.Exists(partPath)) File.Delete(partPath);
                await Task.Delay(TimeSpan.FromSeconds(attempt * 2), cancellationToken);
            }
            catch (Exception ex) when (
                attempt < MaxAttempts && ex is HttpRequestException or IOException or OperationCanceledException)
            {
                await Task.Delay(TimeSpan.FromSeconds(attempt * 2), cancellationToken);
            }
        }
    }

    private async Task<string> GetStringWithRetryAsync(string url, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                using var response = await _http.GetAsync(url, cancellationToken);
                TrackRateLimit(response);
                await EnsureNotRateLimited(response);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (
                attempt < MaxAttempts && ex is HttpRequestException or IOException or OperationCanceledException)
            {
                Log.Warning("GitHub request retry (attempt {Attempt}): {Message}", attempt, ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(attempt * 2), cancellationToken);
            }
        }
    }

    private static async Task EnsureNotRateLimited(HttpResponseMessage response)
    {
        if (response.StatusCode == (HttpStatusCode)429)
            throw new HttpRequestException("GitHub API rate limit exceeded");

        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            var remaining = response.Headers.GetValues("X-RateLimit-Remaining").FirstOrDefault();
            if (remaining == "0")
                throw new HttpRequestException("GitHub API rate limit exceeded");
        }
    }

    private void TrackRateLimit(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("X-RateLimit-Remaining", out var remainingValues)) return;

        var remainingStr = remainingValues.FirstOrDefault();
        if (int.TryParse(remainingStr, out var remaining))
        {
            _remainingRequests = remaining;
            if (remaining <= 5)
                Log.Warning("GitHub API rate limit low: {Remaining} requests remaining", remaining);
        }

        if (remainingStr != "0") return;
        if (!response.Headers.TryGetValues("X-RateLimit-Reset", out var resetValues)) return;

        var resetUnix = long.TryParse(resetValues.FirstOrDefault(), out var r) ? r : 0;
        if (resetUnix > 0)
        {
            _rateLimitReset = DateTimeOffset.FromUnixTimeSeconds(resetUnix);
            Log.Warning("GitHub API rate limited until {Reset}", _rateLimitReset);
        }
    }

    private static ReleaseInfo Map(ReleaseDto dto) => new()
    {
        TagName = dto.TagName,
        IsPreRelease = dto.PreRelease,
        PublishedAt = dto.PublishedAt ?? DateTimeOffset.MinValue,
        Assets = dto.Assets
            .Select(a => new ReleaseAsset(a.Name, a.DownloadUrl, a.Size, ParseDigest(a.Digest)))
            .ToList(),
    };

    private static string? ParseDigest(string? digest)
    {
        if (string.IsNullOrEmpty(digest)) return null;
        const string prefix = "sha256:";
        return digest.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? digest[prefix.Length..]
            : null;
    }

    private sealed class ReleaseDto
    {
        [JsonPropertyName("tag_name")] public string TagName { get; set; } = "";
        [JsonPropertyName("prerelease")] public bool PreRelease { get; set; }
        [JsonPropertyName("published_at")] public DateTimeOffset? PublishedAt { get; set; }
        [JsonPropertyName("assets")] public List<AssetDto> Assets { get; set; } = [];
    }

    private sealed class AssetDto
    {
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("browser_download_url")] public string DownloadUrl { get; set; } = "";
        [JsonPropertyName("size")] public long Size { get; set; }
        [JsonPropertyName("digest")] public string? Digest { get; set; }
    }
}
