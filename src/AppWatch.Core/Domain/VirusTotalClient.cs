using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace AppWatch.Core.Domain;

public sealed record VirusTotalReport(
    bool IsKnown,
    int MaliciousCount,
    int SuspiciousCount,
    int HarmlessCount,
    int UndetectedCount,
    string? Permalink
)
{
    public bool IsSafe => MaliciousCount == 0 && SuspiciousCount == 0;
}

public class VirusTotalClient
{
    private readonly HttpClient _http;
    private readonly string? _apiKey;

    public VirusTotalClient(HttpClient http, string? apiKey = null)
    {
        _http = http;
        _apiKey = apiKey;
    }

    /// <summary>
    /// Checks a SHA-256 hash against VirusTotal API v3 without uploading the file.
    /// Free tier: 4 lookups / min.
    /// </summary>
    public virtual async Task<VirusTotalReport?> CheckFileHashAsync(string sha256Hex, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_apiKey) || string.IsNullOrWhiteSpace(sha256Hex))
        {
            return null;
        }

        var url = $"https://www.virustotal.com/api/v3/files/{sha256Hex.ToLowerInvariant()}";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("x-apikey", _apiKey);

        var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // Unknown file in VT database
            return new VirusTotalReport(false, 0, 0, 0, 0, null);
        }

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var vtResponse = await response.Content.ReadFromJsonAsync<VtResponseDto>(cancellationToken: cancellationToken).ConfigureAwait(false);
        var stats = vtResponse?.Data?.Attributes?.LastAnalysisStats;
        if (stats is null)
        {
            return new VirusTotalReport(true, 0, 0, 0, 0, null);
        }

        var permalink = $"https://www.virustotal.com/gui/file/{sha256Hex.ToLowerInvariant()}";
        return new VirusTotalReport(
            IsKnown: true,
            MaliciousCount: stats.Malicious,
            SuspiciousCount: stats.Suspicious,
            HarmlessCount: stats.Harmless,
            UndetectedCount: stats.Undetected,
            Permalink: permalink
        );
    }

    private sealed class VtResponseDto
    {
        [JsonPropertyName("data")]
        public VtDataDto? Data { get; set; }
    }

    private sealed class VtDataDto
    {
        [JsonPropertyName("attributes")]
        public VtAttributesDto? Attributes { get; set; }
    }

    private sealed class VtAttributesDto
    {
        [JsonPropertyName("last_analysis_stats")]
        public VtStatsDto? LastAnalysisStats { get; set; }
    }

    private sealed class VtStatsDto
    {
        [JsonPropertyName("malicious")]
        public int Malicious { get; set; }

        [JsonPropertyName("suspicious")]
        public int Suspicious { get; set; }

        [JsonPropertyName("harmless")]
        public int Harmless { get; set; }

        [JsonPropertyName("undetected")]
        public int Undetected { get; set; }
    }
}
