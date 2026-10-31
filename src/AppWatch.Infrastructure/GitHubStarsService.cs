using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace AppWatch.Infrastructure;

/// <summary>
/// GitHub Stars import: fetches a user's starred repos and their latest releases.
/// Supports an optional PAT token (raises the API limit from 60 to 5000 req/hour)
/// and tracks the rate-limit state so the caller can stop gracefully.
/// </summary>
public sealed class GitHubStarsService
{
    private static readonly HttpClient Http = new() { Timeout = global::System.TimeSpan.FromSeconds(30) };

    private readonly string? _token;

    public GitHubStarsService(string? token = null) => _token = string.IsNullOrWhiteSpace(token) ? null : token.Trim();

    /// <summary>Remaining API requests in the current window (updated on every call).</summary>
    public int RateLimitRemaining { get; private set; } = int.MaxValue;

    /// <summary>True once GitHub answered 403 with an exhausted quota.</summary>
    public bool RateLimitExhausted { get; private set; }

    /// <summary>Fetches all starred repositories for a user (paginated).</summary>
    public async Task<List<GitHubStarredRepo>> GetStarredReposAsync(string username, CancellationToken ct = default)
    {
        var repos = new List<GitHubStarredRepo>();
        var url = $"https://api.github.com/users/{username}/starred?per_page=100";

        while (!string.IsNullOrEmpty(url))
        {
            ct.ThrowIfCancellationRequested();
            using var response = await SendAsync(url, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"GitHub API {(int)response.StatusCode}: {response.ReasonPhrase}");

            var page = await response.Content.ReadFromJsonAsync<List<GitHubStarredRepo>>(JsonOptions, ct).ConfigureAwait(false)
                ?? new List<GitHubStarredRepo>();

            if (page.Count == 0) break;
            repos.AddRange(page);

            url = GetNextPageUrl(response.Headers);
        }

        return repos;
    }

    /// <summary>
    /// Fetches the latest release (with assets) for a repo.
    /// Returns null when the repo has no releases. Returns null AND sets
    /// <see cref="RateLimitExhausted"/> when the API quota is exhausted.
    /// </summary>
    public async Task<GitHubRelease?> GetLatestReleaseAsync(string owner, string repo, CancellationToken ct = default)
    {
        var url = $"https://api.github.com/repos/{owner}/{repo}/releases/latest";
        using var response = await SendAsync(url, ct).ConfigureAwait(false);

        if (response.StatusCode == global::System.Net.HttpStatusCode.Forbidden && RateLimitRemaining <= 1)
        {
            RateLimitExhausted = true;
            return null;
        }

        if (response.StatusCode == global::System.Net.HttpStatusCode.NotFound)
            return null; // no releases

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"GitHub API {(int)response.StatusCode}: {response.ReasonPhrase}");

        return await response.Content.ReadFromJsonAsync<GitHubRelease>(JsonOptions, ct).ConfigureAwait(false);
    }

    private async Task<HttpResponseMessage> SendAsync(string url, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("User-Agent", "AppWatch/1.0 (+https://github.com/skllfi2/AppWatch)");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        if (_token is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);

        var response = await Http.SendAsync(request, ct).ConfigureAwait(false);
        TrackRateLimit(response);
        return response;
    }

    private void TrackRateLimit(HttpResponseMessage response)
    {
        if (response.Headers.TryGetValues("X-RateLimit-Remaining", out var values))
        {
            var raw = values.FirstOrDefault();
            if (int.TryParse(raw, out var remaining))
                RateLimitRemaining = remaining;
        }
    }

    private static string? GetNextPageUrl(HttpResponseHeaders headers)
    {
        if (!headers.TryGetValues("Link", out var links)) return null;
        foreach (var link in links)
        {
            var parts = link.Split(',');
            foreach (var part in parts)
            {
                if (part.Contains("rel=\"next\""))
                {
                    var start = part.IndexOf('<') + 1;
                    var end = part.IndexOf('>');
                    return part.Substring(start, end - start);
                }
            }
        }
        return null;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new global::System.Text.Json.Serialization.JsonStringEnumConverter() }
    };
}

/// <summary>Repo stub returned by the starred endpoint. NOTE: <c>owner</c> is a nested object.</summary>
public sealed class GitHubStarredRepo
{
    public string Name { get; set; } = "";
    public string FullName { get; set; } = "";
    public string? Description { get; set; }
    public string HtmlUrl { get; set; } = "";
    public bool Fork { get; set; }
    public int StargazersCount { get; set; }
    public string? Language { get; set; }
    public GitHubOwner? Owner { get; set; }

    /// <summary>Flat owner login for catalog entries ("owner/repo" pairs).</summary>
    [JsonIgnore]
    public string OwnerLogin =>
        Owner?.Login
        ?? (FullName.Contains('/') ? FullName.Split('/')[0] : "");
}

public sealed class GitHubOwner
{
    public string Login { get; set; } = "";
    public string AvatarUrl { get; set; } = "";
}

public record GitHubRelease(
    string TagName,
    string? Name,
    string HtmlUrl,
    bool Prerelease,
    GitHubReleaseAsset[]? Assets);

public record GitHubReleaseAsset(
    string Name,
    long Size,
    string BrowserDownloadUrl,
    string? ContentType);
