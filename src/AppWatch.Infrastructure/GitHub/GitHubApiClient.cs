using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using AppWatch.Core.Abstractions;
using AppWatch.Core.Domain;
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
    private readonly IReadOnlyList<string> _downloadMirrors;
    private DateTimeOffset _rateLimitReset = DateTimeOffset.MinValue;
    private int? _remainingRequests;

    public bool IsRateLimited => DateTimeOffset.UtcNow < _rateLimitReset;
    public DateTimeOffset? RateLimitResetUtc => _rateLimitReset == DateTimeOffset.MinValue ? null : _rateLimitReset;
    public int? RemainingRequests => _remainingRequests;

    public GitHubApiClient(string? token = null, IEnumerable<string>? downloadMirrors = null)
    {
        _http = new HttpClient();
        // P0 live-fix: the API calls use RELATIVE urls (repos/{owner}/{repo}/releases) вЂ”
        // without BaseAddress every call died with "request URI must be absolute"
        // (the GitHub path of the app never actually worked until this line).
        _http.BaseAddress = new Uri("https://api.github.com/");
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("AppWatch/0.1");
        _http.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        if (!string.IsNullOrWhiteSpace(token))
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        _downloadMirrors = downloadMirrors?.ToList() ?? [];
    }

    public async Task<GitHubRepoInfo?> GetRepoInfoAsync(string owner, string repo, CancellationToken cancellationToken = default)
    {
        var url = $"repos/{owner}/{repo}";
        if (_cache.TryGetValue<GitHubRepoInfo>(url, out var cached) && cached is not null)
            return cached;

        if (IsRateLimited)
            throw new HttpRequestException($"GitHub API rate limit exceeded until {RateLimitResetUtc:HH:mm:ss}");

        var json = await GetStringWithRetryAsync(url, cancellationToken).ConfigureAwait(false);
        var dto = JsonSerializer.Deserialize<RepoDto>(json, JsonOptions);
        if (dto is null) return null;

        string? iconUrl = null;
        try
        {
            iconUrl = await TryFindRepoIconUrlAsync(owner, repo, dto.DefaultBranch, cancellationToken).ConfigureAwait(false);
        }
        catch { }

        var info = new GitHubRepoInfo
        {
            Name = dto.Name,
            Description = dto.Description,
            HtmlUrl = dto.HtmlUrl,
            Homepage = dto.Homepage,
            AvatarUrl = iconUrl ?? dto.Owner.AvatarUrl,
            OwnerLogin = dto.Owner.Login,
            OwnerAvatarUrl = dto.Owner.AvatarUrl,
            IconUrl = iconUrl,
        };

        _cache.Set(url, info, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = CacheDuration,
            Size = 1,
        });
        return info;
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

        var json = await GetStringWithRetryAsync(url, cancellationToken).ConfigureAwait(false);
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

        // B8: rotate github.com в†’ mirror 1 в†’ mirror 2; the .part resume file is
        // shared across candidates (mirrors serve the identical asset bytes).
        var candidates = DownloadMirrors.Candidates(url, _downloadMirrors);
        Exception? lastError = null;

        foreach (var candidate in candidates)
        {
            for (var attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                try
                {
                    await DownloadFromAsync(candidate, partPath, progress, cancellationToken).ConfigureAwait(false);
                    File.Move(partPath, destinationPath, overwrite: true);
                    return;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable)
                {
                    // stale/oversized .part вЂ” drop it and retry this source from scratch
                    if (File.Exists(partPath)) File.Delete(partPath);
                    lastError = ex;
                    if (attempt >= MaxAttempts) break;
                    await Task.Delay(TimeSpan.FromSeconds(attempt * 2), cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is HttpRequestException or IOException or OperationCanceledException)
                {
                    lastError = ex;
                    if (attempt >= MaxAttempts) break;
                    await Task.Delay(TimeSpan.FromSeconds(attempt * 2), cancellationToken).ConfigureAwait(false);
                }
            }

            if (!ReferenceEquals(candidate, candidates[^1]))
                Log.Warning("download via {Host} failed after {Attempts} attempts; rotating to next source",
                    HostOf(candidate), MaxAttempts);
        }

        throw lastError ?? new HttpRequestException("download failed: no source available");
    }

    private async Task DownloadFromAsync(
        string url, string partPath, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        var existing = File.Exists(partPath) ? new FileInfo(partPath).Length : 0;
        if (existing > 0)
            request.Headers.Range = new RangeHeaderValue(existing, null);

        using var response = await _http.SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        // a server that ignores Range answers 200 with the full body вЂ” restart from scratch
        var append = existing > 0 && response.StatusCode == HttpStatusCode.PartialContent;
        long baseOffset = append ? existing : 0;
        if (existing > 0 && !append) File.Delete(partPath);

        var total = (response.Content.Headers.ContentLength ?? 0) + baseOffset;
        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var target = new FileStream(
            partPath, append ? FileMode.Append : FileMode.Create, FileAccess.Write);

        var buffer = new byte[81920];
        long written = baseOffset;
        int read;
        while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            written += read;
            if (total > 0) progress?.Report(written * 100.0 / total);
        }
    }

    private static string HostOf(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Host : "unknown-host";

    private async Task<string> GetStringWithRetryAsync(string url, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                using var response = await _http.GetAsync(url, cancellationToken).ConfigureAwait(false);
                TrackRateLimit(response);
                await EnsureNotRateLimited(response).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (
                attempt < MaxAttempts && ex is HttpRequestException or IOException or OperationCanceledException)
            {
                Log.Warning("GitHub request retry (attempt {Attempt}): {Message}", attempt, ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(attempt * 2), cancellationToken).ConfigureAwait(false);
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
        Body = dto.Body,
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

    private static readonly string[] CommonIconPaths =
    [
        "assets/icon.png",
        "assets/logo.png",
        "assets/appicon.png",
        "icon.png",
        "logo.png",
        "resources/icon.png",
        "resources/logo.png",
        "res/icon.png",
        "src/assets/icon.png",
        "src/assets/logo.png",
        ".github/icon.png",
        ".github/logo.png",
        "docs/icon.png",
        "docs/logo.png",
        "assets/icon.ico",
        "icon.ico"
    ];

    private async Task<string?> TryFindRepoIconUrlAsync(string owner, string repo, string? branch, CancellationToken ct)
    {
        var targetBranch = !string.IsNullOrWhiteSpace(branch) ? branch : "HEAD";
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(3));

        var tasks = CommonIconPaths.Select(async p =>
        {
            var rawUrl = $"https://raw.githubusercontent.com/{owner}/{repo}/{targetBranch}/{p}";
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Head, rawUrl);
                using var res = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cts.Token).ConfigureAwait(false);
                if (res.IsSuccessStatusCode)
                {
                    var ctType = res.Content.Headers.ContentType?.MediaType;
                    if (ctType is null ||
                        ctType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ||
                        ctType == "application/octet-stream" ||
                        rawUrl.EndsWith(".ico", StringComparison.OrdinalIgnoreCase))
                    {
                        return rawUrl;
                    }
                }
            }
            catch { }
            return null;
        }).ToList();

        try
        {
            var results = await Task.WhenAll(tasks).ConfigureAwait(false);
            return results.FirstOrDefault(r => r is not null);
        }
        catch
        {
            return null;
        }
    }

    private sealed class ReleaseDto
    {
        [JsonPropertyName("tag_name")] public string TagName { get; set; } = "";
        [JsonPropertyName("prerelease")] public bool PreRelease { get; set; }
        [JsonPropertyName("published_at")] public DateTimeOffset? PublishedAt { get; set; }
        [JsonPropertyName("body")] public string? Body { get; set; }
        [JsonPropertyName("assets")] public List<AssetDto> Assets { get; set; } = [];
    }

    private sealed class AssetDto
    {
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("browser_download_url")] public string DownloadUrl { get; set; } = "";
        [JsonPropertyName("size")] public long Size { get; set; }
        [JsonPropertyName("digest")] public string? Digest { get; set; }
    }

    private sealed class RepoDto
    {
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("description")] public string? Description { get; set; }
        [JsonPropertyName("html_url")] public string HtmlUrl { get; set; } = "";
        [JsonPropertyName("homepage")] public string? Homepage { get; set; }
        [JsonPropertyName("default_branch")] public string? DefaultBranch { get; set; }
        [JsonPropertyName("owner")] public OwnerDto Owner { get; set; } = new();
    }

    private sealed class OwnerDto
    {
        [JsonPropertyName("login")] public string Login { get; set; } = "";
        [JsonPropertyName("avatar_url")] public string AvatarUrl { get; set; } = "";
    }
}
