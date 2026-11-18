using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using AppWatch.Core.Abstractions;
using AppWatch.Core.Domain.Models;

namespace AppWatch.Core.Domain;

/// <summary>
/// Fetches latest releases by inspecting RSS/Atom feeds or scraping version patterns from HTML web pages.
/// </summary>
public sealed class FeedWebReleaseClient
{
    private readonly HttpClient _httpClient;

    // Standard SemVer / version matcher: e.g. v1.2.3, 2026.1, 4.5.6-beta
    private static readonly Regex DefaultVersionRegex = new(
        @"(?:v|ver|version)?\s*(?<version>\d+(?:\.\d+)+(?:-[a-zA-Z0-9\.]+)?)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public FeedWebReleaseClient(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "AppWatch/2.0 (Windows NT 10.0; Win64; x64)");
        }
    }

    /// <summary>
    /// Checks a website URL or RSS/Atom feed URL to extract the latest release info.
    /// </summary>
    public async Task<ReleaseInfo?> GetLatestReleaseAsync(
        string url,
        string? customRegexPattern = null,
        string? downloadUrlTemplate = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var mediaType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;

        // Try parsing as RSS / Atom feed first if content starts with XML or content-type is xml/rss/atom
        if (content.TrimStart().StartsWith('<') && 
            (mediaType.Contains("xml", StringComparison.OrdinalIgnoreCase) || 
             mediaType.Contains("atom", StringComparison.OrdinalIgnoreCase) || 
             content.Contains("<feed", StringComparison.OrdinalIgnoreCase) || 
             content.Contains("<rss", StringComparison.OrdinalIgnoreCase)))
        {
            var feedRelease = TryParseFeed(content, customRegexPattern, downloadUrlTemplate);
            if (feedRelease is not null)
                return feedRelease;
        }

        // Web scraping fallback
        return ParseHtmlPage(content, customRegexPattern, downloadUrlTemplate, url);
    }

    private static ReleaseInfo? TryParseFeed(string xml, string? customRegexPattern, string? downloadUrlTemplate)
    {
        try
        {
            var doc = XDocument.Parse(xml);
            var regex = !string.IsNullOrWhiteSpace(customRegexPattern) 
                ? new Regex(customRegexPattern, RegexOptions.IgnoreCase) 
                : DefaultVersionRegex;

            // Check Atom entries
            XNamespace atom = "http://www.w3.org/2005/Atom";
            var entries = doc.Descendants(atom + "entry");
            foreach (var entry in entries)
            {
                var title = entry.Element(atom + "title")?.Value ?? string.Empty;
                var match = regex.Match(title);
                if (match.Success)
                {
                    var ver = match.Groups["version"].Success ? match.Groups["version"].Value : match.Groups[1].Value;
                    var link = entry.Element(atom + "link")?.Attribute("href")?.Value ?? string.Empty;
                    var published = entry.Element(atom + "published")?.Value ?? entry.Element(atom + "updated")?.Value;
                    DateTimeOffset.TryParse(published, out var dto);

                    var downloadUrl = !string.IsNullOrWhiteSpace(downloadUrlTemplate)
                        ? downloadUrlTemplate.Replace("{version}", ver, StringComparison.OrdinalIgnoreCase)
                        : link;

                    return new ReleaseInfo
                    {
                        TagName = ver,
                        PublishedAt = dto != default ? dto : DateTimeOffset.UtcNow,
                        IsPreRelease = false,
                        Body = entry.Element(atom + "content")?.Value ?? title,
                        Assets = [new ReleaseAsset(Path.GetFileName(downloadUrl), downloadUrl, 0, null)]
                    };
                }
            }

            // Check RSS items
            var items = doc.Descendants("item");
            foreach (var item in items)
            {
                var title = item.Element("title")?.Value ?? string.Empty;
                var match = regex.Match(title);
                if (match.Success)
                {
                    var ver = match.Groups["version"].Success ? match.Groups["version"].Value : match.Groups[1].Value;
                    var link = item.Element("link")?.Value ?? string.Empty;
                    var pubDate = item.Element("pubDate")?.Value;
                    DateTimeOffset.TryParse(pubDate, out var dto);

                    var downloadUrl = !string.IsNullOrWhiteSpace(downloadUrlTemplate)
                        ? downloadUrlTemplate.Replace("{version}", ver, StringComparison.OrdinalIgnoreCase)
                        : link;

                    return new ReleaseInfo
                    {
                        TagName = ver,
                        PublishedAt = dto != default ? dto : DateTimeOffset.UtcNow,
                        IsPreRelease = false,
                        Body = item.Element("description")?.Value ?? title,
                        Assets = [new ReleaseAsset(Path.GetFileName(downloadUrl), downloadUrl, 0, null)]
                    };
                }
            }
        }
        catch
        {
            // Ignore XML parsing errors and allow fallback
        }

        return null;
    }

    private static ReleaseInfo? ParseHtmlPage(string html, string? customRegexPattern, string? downloadUrlTemplate, string pageUrl)
    {
        var regex = !string.IsNullOrWhiteSpace(customRegexPattern)
            ? new Regex(customRegexPattern, RegexOptions.IgnoreCase)
            : DefaultVersionRegex;

        var match = regex.Match(html);
        if (!match.Success)
            return null;

        var ver = match.Groups["version"].Success ? match.Groups["version"].Value : match.Groups[1].Value;
        var downloadUrl = !string.IsNullOrWhiteSpace(downloadUrlTemplate)
            ? downloadUrlTemplate.Replace("{version}", ver, StringComparison.OrdinalIgnoreCase)
            : pageUrl;

        return new ReleaseInfo
        {
            TagName = ver,
            PublishedAt = DateTimeOffset.UtcNow,
            IsPreRelease = false,
            Body = $"Web page parsed version {ver}",
            Assets = [new ReleaseAsset(Path.GetFileName(downloadUrl), downloadUrl, 0, null)]
        };
    }
}
