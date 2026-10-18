using AppWatch.Core.Domain;
using AppWatch.Core.Domain.Models;
using Xunit;

namespace AppWatch.Core.Tests;

/// <summary>
/// User-extendable catalog: merge logic (seed + user entries).
/// User entries override seed by owner/repo; user-only entries appended alphabetically.
/// </summary>
public class CatalogMergerTests
{
    private static CatalogEntry Entry(string name, string owner, string repo, string? description = null) => new()
    {
        Name = name,
        RepoOwner = owner,
        RepoName = repo,
        Description = description,
    };

    [Fact]
    public void Merge_EmptyUser_ReturnsSeed()
    {
        var seed = new[] { Entry("yt-dlp", "yt-dlp", "yt-dlp"), Entry("Vesktop", "Vencord", "Vesktop") };
        var merged = CatalogMerger.Merge(seed, []);
        Assert.Equal(2, merged.Count);
        Assert.Equal("yt-dlp", merged[0].Name);
        Assert.Equal("Vesktop", merged[1].Name);
    }

    [Fact]
    public void Merge_UserOverridesSeed()
    {
        var seed = new[] { Entry("yt-dlp", "yt-dlp", "yt-dlp", "original description") };
        var user = new[] { Entry("yt-dlp", "yt-dlp", "yt-dlp", "my custom description") };
        var merged = CatalogMerger.Merge(seed, user);
        Assert.Single(merged);
        Assert.Equal("my custom description", merged[0].Description);
    }

    [Fact]
    public void Merge_UserOnlyEntry_Appended()
    {
        var seed = new[] { Entry("yt-dlp", "yt-dlp", "yt-dlp") };
        var user = new[] { Entry("MyTool", "me", "mytool") };
        var merged = CatalogMerger.Merge(seed, user);
        Assert.Equal(2, merged.Count);
        Assert.Equal("yt-dlp", merged[0].Name);
        Assert.Equal("MyTool", merged[1].Name); // appended after seed
    }

    [Fact]
    public void Merge_MultipleUserOnlyEntries_AppendedAlphabetically()
    {
        var seed = new[] { Entry("zapret", "Flowseal", "zapret") };
        var user = new[]
        {
            Entry("ZooTool", "me", "zootool"),
            Entry("AlphaTool", "me", "alphatool"),
        };
        var merged = CatalogMerger.Merge(seed, user);
        Assert.Equal(3, merged.Count);
        Assert.Equal("zapret", merged[0].Name);
        Assert.Equal("AlphaTool", merged[1].Name); // alphabetical
        Assert.Equal("ZooTool", merged[2].Name);
    }

    [Fact]
    public void Merge_CaseInsensitiveOverride()
    {
        // seed uses "yt-dlp", user file has "YT-DLP" (different case) — should still override
        var seed = new[] { Entry("yt-dlp", "yt-dlp", "yt-dlp") };
        var user = new[] { Entry("Yt-Dlp", "yt-dlp", "yt-dlp", "overridden") };
        var merged = CatalogMerger.Merge(seed, user);
        Assert.Single(merged);
        Assert.Equal("overridden", merged[0].Description);
    }

    [Fact]
    public void IsUserEntry_MatchesCaseInsensitive()
    {
        var user = new[] { Entry("MyTool", "Me", "MyTool") };
        Assert.True(CatalogMerger.IsUserEntry(user, "me", "mytool"));
        Assert.True(CatalogMerger.IsUserEntry(user, "ME", "MYTOOL"));
        Assert.False(CatalogMerger.IsUserEntry(user, "other", "repo"));
    }

    [Fact]
    public void IsUserEntry_EmptyList_ReturnsFalse()
    {
        Assert.False(CatalogMerger.IsUserEntry([], "any", "repo"));
    }
}
