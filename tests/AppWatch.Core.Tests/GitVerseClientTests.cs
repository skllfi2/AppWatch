using AppWatch.Core.Domain.Models;
using AppWatch.Infrastructure.Git;
using Xunit;

namespace AppWatch.Core.Tests;

public class GitVerseClientTests
{
    [Fact]
    public void CanHandle_GitVerseUrls_ReturnsTrue()
    {
        var client = new GitVerseApiClient();

        Assert.True(client.CanHandle("https://gitverse.ru/sber/cool-app"));
        Assert.True(client.CanHandle("gitverse.ru"));
        Assert.False(client.CanHandle("https://github.com/user/repo"));
    }

    [Fact]
    public void ProviderName_IsGitVerse()
    {
        var client = new GitVerseApiClient();
        Assert.Equal("GitVerse", client.ProviderName);
    }

    [Fact]
    public void MapGiteaRelease_ProducesStandardReleaseInfo()
    {
        var giteaJson = """
        {
          "tag_name": "v1.2.0",
          "name": "Release 1.2.0",
          "prerelease": false,
          "published_at": "2026-10-01T12:00:00Z",
          "body": "Bugfixes and features",
          "assets": [
            {
              "name": "coolapp-windows-x64.zip",
              "browser_download_url": "https://gitverse.ru/attachments/123",
              "size": 10485760
            }
          ]
        }
        """;

        var release = GitVerseApiClient.ParseGiteaRelease(giteaJson);

        Assert.NotNull(release);
        Assert.Equal("v1.2.0", release.TagName);
        Assert.False(release.IsPreRelease);
        Assert.Equal("Bugfixes and features", release.Body);
        Assert.Single(release.Assets);
        Assert.Equal("coolapp-windows-x64.zip", release.Assets[0].Name);
        Assert.Equal("https://gitverse.ru/attachments/123", release.Assets[0].DownloadUrl);
        Assert.Equal(10485760, release.Assets[0].Size);
    }
}
