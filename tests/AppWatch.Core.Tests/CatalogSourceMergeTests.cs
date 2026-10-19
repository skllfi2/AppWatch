using AppWatch.Core.Domain;
using AppWatch.Core.Domain.Models;
using Xunit;

namespace AppWatch.Core.Tests;

/// <summary>
/// Multi-source catalog merge: an ordered list of sources, later sources
/// override earlier ones by owner/repo. Tracks which source each entry came from.
/// </summary>
public class CatalogSourceMergeTests
{
    private static CatalogEntry E(string name, string owner, string repo) => new()
    {
        Name = name,
        RepoOwner = owner,
        RepoName = repo,
    };

    [Fact]
    public void MergeSources_SingleSource_ReturnsAllEntries()
    {
        var sources = new List<(string, IReadOnlyList<CatalogEntry>)>
        {
            ("seed", new[] { E("yt-dlp", "yt-dlp", "yt-dlp"), E("Vesktop", "Vencord", "Vesktop") }),
        };
        var (entries, sourceMap) = CatalogMerger.MergeSources(sources);
        Assert.Equal(2, entries.Count);
        Assert.Equal("seed", sourceMap["yt-dlp/yt-dlp"]);
        Assert.Equal("seed", sourceMap["Vencord/Vesktop"]);
    }

    [Fact]
    public void MergeSources_LaterSourceOverridesEarlier()
    {
        var sources = new List<(string, IReadOnlyList<CatalogEntry>)>
        {
            ("seed", new[] { E("yt-dlp", "yt-dlp", "yt-dlp") }),
            ("community", new[] { E("yt-dlp", "yt-dlp", "yt-dlp") }), // same repo, different source
        };
        var (entries, sourceMap) = CatalogMerger.MergeSources(sources);
        Assert.Single(entries);
        Assert.Equal("community", sourceMap["yt-dlp/yt-dlp"]); // later source wins
    }

    [Fact]
    public void MergeSources_ThreeSources_EntryFromLast()
    {
        var sources = new List<(string, IReadOnlyList<CatalogEntry>)>
        {
            ("seed", new[] { E("App", "a", "b") }),
            ("user", new[] { E("App", "a", "b") }),       // overrides seed
            ("community", new[] { E("App", "a", "b") }),   // overrides user
        };
        var (entries, sourceMap) = CatalogMerger.MergeSources(sources);
        Assert.Single(entries);
        Assert.Equal("community", sourceMap["a/b"]); // last source wins
    }

    [Fact]
    public void MergeSources_DisjointSources_AllEntriesPresent()
    {
        var sources = new List<(string, IReadOnlyList<CatalogEntry>)>
        {
            ("seed", new[] { E("yt-dlp", "yt-dlp", "yt-dlp") }),
            ("DPI tools", new[] { E("zapret", "Flowseal", "zapret") }),
            ("Dev tools", new[] { E("PowerToys", "microsoft", "PowerToys") }),
        };
        var (entries, sourceMap) = CatalogMerger.MergeSources(sources);
        Assert.Equal(3, entries.Count);
        Assert.Equal("seed", sourceMap["yt-dlp/yt-dlp"]);
        Assert.Equal("DPI tools", sourceMap["Flowseal/zapret"]);
        Assert.Equal("Dev tools", sourceMap["microsoft/PowerToys"]);
    }

    [Fact]
    public void MergeSources_PartialOverride_KeepsOtherEntriesFromFirstSource()
    {
        var sources = new List<(string, IReadOnlyList<CatalogEntry>)>
        {
            ("seed", new[] { E("A", "o", "a"), E("B", "o", "b"), E("C", "o", "c") }),
            ("override", new[] { E("B*", "o", "b") }), // only overrides B
        };
        var (entries, sourceMap) = CatalogMerger.MergeSources(sources);
        Assert.Equal(3, entries.Count);
        // A and C from seed, B from override
        Assert.Equal("seed", sourceMap["o/a"]);
        Assert.Equal("override", sourceMap["o/b"]);
        Assert.Equal("seed", sourceMap["o/c"]);
        // B's name is the overridden version
        Assert.Equal("B*", entries.First(e => e.RepoName == "b").Name);
    }

    [Fact]
    public void MergeSources_EmptySources_ReturnsEmpty()
    {
        var (entries, sourceMap) = CatalogMerger.MergeSources([]);
        Assert.Empty(entries);
        Assert.Empty(sourceMap);
    }

    [Fact]
    public void MergeSources_InsertionOrderPreserved()
    {
        var sources = new List<(string, IReadOnlyList<CatalogEntry>)>
        {
            ("seed", new[] { E("Alpha", "o", "alpha"), E("Beta", "o", "beta") }),
            ("extra", new[] { E("Gamma", "x", "gamma") }),
        };
        var (entries, _) = CatalogMerger.MergeSources(sources);
        // seed order preserved, then extra appended in its own order
        Assert.Equal("Alpha", entries[0].Name);
        Assert.Equal("Beta", entries[1].Name);
        Assert.Equal("Gamma", entries[2].Name);
    }
}
