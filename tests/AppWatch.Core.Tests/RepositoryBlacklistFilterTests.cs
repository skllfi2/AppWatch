using AppWatch.Core.Domain;
using Xunit;

namespace AppWatch.Core.Tests;

public class RepositoryBlacklistFilterTests
{
    [Theory]
    [InlineData("dotnet", "docs", true)]
    [InlineData("facebook", "react-docs", true)]
    [InlineData("vinta", "awesome-python", true)]
    [InlineData("sindresorhus", "awesome", true)]
    [InlineData("code-yeongyu", "oh-my-openagent", false)]
    [InlineData("notepad-plus-plus", "notepad-plus-plus", false)]
    [InlineData("BurntSushi", "ripgrep", false)]
    public void IsBlacklisted_DefaultPatterns_IdentifiesNoise(string owner, string name, bool expected)
    {
        var result = RepositoryBlacklistFilter.IsBlacklisted(owner, name);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void IsBlacklisted_CustomWildcardPatterns_MatchesCorrectly()
    {
        var custom = new[] { "*-temp", "experimental/*" };

        Assert.True(RepositoryBlacklistFilter.IsBlacklisted("user", "my-repo-temp", custom));
        Assert.True(RepositoryBlacklistFilter.IsBlacklisted("experimental", "cool-tool", custom));
        Assert.False(RepositoryBlacklistFilter.IsBlacklisted("user", "production-app", custom));
    }
}
