using AppWatch.Core.Domain.Models;
using Xunit;

namespace AppWatch.Core.Tests;

public class TaxonomyModelTests
{
    [Fact]
    public void TrackedApp_Defaults_HasUncategorizedAndGitHubRelease()
    {
        var app = new TrackedApp
        {
            Name = "Demo",
            RepoOwner = "owner",
            RepoName = "repo"
        };

        Assert.Equal(AppCategory.Uncategorized, app.Category);
        Assert.Equal(AppSourceOrigin.GitHubRelease, app.SourceOrigin);
        Assert.NotNull(app.Tags);
        Assert.Empty(app.Tags);
        Assert.False(app.IsInPlaceInstall);
    }
}
