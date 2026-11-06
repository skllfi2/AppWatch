using AppWatch.Core.Domain.Models;
using AppWatch.Infrastructure.Git;
using Xunit;

namespace AppWatch.Core.Tests;

public class GitLabClientTests
{
    [Fact]
    public void CanHandle_GitLabUrls_ReturnsTrue()
    {
        var client = new GitLabApiClient();

        Assert.True(client.CanHandle("https://gitlab.com/group/project"));
        Assert.True(client.CanHandle("gitlab.com"));
        Assert.False(client.CanHandle("https://github.com/user/repo"));
        Assert.False(client.CanHandle("https://gitverse.ru/user/repo"));
    }

    [Fact]
    public void ProviderName_IsGitLab()
    {
        var client = new GitLabApiClient();
        Assert.Equal("GitLab", client.ProviderName);
    }

    [Fact]
    public void MapGitLabRelease_ProducesStandardReleaseInfo()
    {
        var gitlabJson = """
        {
          "tag_name": "v2.5.0",
          "name": "Release 2.5.0",
          "description": "Production release notes",
          "released_at": "2026-10-15T09:30:00Z",
          "assets": {
            "count": 1,
            "links": [
              {
                "name": "my-tool-windows-x64.zip",
                "url": "https://gitlab.com/group/project/uploads/abc/my-tool-windows-x64.zip",
                "link_type": "package"
              }
            ]
          }
        }
        """;

        var release = GitLabApiClient.ParseGitLabRelease(gitlabJson);

        Assert.NotNull(release);
        Assert.Equal("v2.5.0", release.TagName);
        Assert.False(release.IsPreRelease);
        Assert.Equal("Production release notes", release.Body);
        Assert.Single(release.Assets);
        Assert.Equal("my-tool-windows-x64.zip", release.Assets[0].Name);
        Assert.Equal("https://gitlab.com/group/project/uploads/abc/my-tool-windows-x64.zip", release.Assets[0].DownloadUrl);
    }
}
