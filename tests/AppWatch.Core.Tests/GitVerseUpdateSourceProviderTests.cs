using AppWatch.Core.Domain.Models;
using AppWatch.Infrastructure.Git;
using AppWatch.Infrastructure.Sources;
using Xunit;

namespace AppWatch.Core.Tests;

public class GitVerseUpdateSourceProviderTests
{
    [Fact]
    public void CanHandle_GitVerseApp_ReturnsTrue()
    {
        var client = new GitVerseApiClient();
        var provider = new GitVerseUpdateSourceProvider(client);

        var app = new TrackedApp
        {
            Name = "GitVerse Tool",
            RepoOwner = "sber",
            RepoName = "tool",
            SourceOrigin = AppSourceOrigin.GitVerse
        };

        Assert.True(provider.CanHandle(app));
        Assert.Equal("gitverse", provider.SourceId);
        Assert.Equal("GitVerse Releases", provider.DisplayName);
    }

    [Fact]
    public void CanHandle_StandardGitHubApp_ReturnsFalse()
    {
        var client = new GitVerseApiClient();
        var provider = new GitVerseUpdateSourceProvider(client);

        var app = new TrackedApp
        {
            Name = "GitHub Tool",
            RepoOwner = "BurntSushi",
            RepoName = "ripgrep",
            SourceOrigin = AppSourceOrigin.GitHubRelease
        };

        Assert.False(provider.CanHandle(app));
    }
}
