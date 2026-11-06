using AppWatch.Core.Domain;
using AppWatch.Core.Domain.Models;
using Xunit;

namespace AppWatch.Core.Tests;

public class ETagReleaseCacheTests
{
    [Fact]
    public void CacheEntry_SavesAndRetrievesEtagAndReleases()
    {
        var cache = new ETagReleaseCache();
        var releases = new List<ReleaseInfo>
        {
            new()
            {
                TagName = "v1.0.0",
                IsPreRelease = false,
                PublishedAt = DateTimeOffset.UtcNow,
                Assets = []
            }
        };

        cache.Update("BurntSushi", "ripgrep", "W/\"etag123\"", releases);

        var hasCached = cache.TryGet("BurntSushi", "ripgrep", out var etag, out var cachedReleases);

        Assert.True(hasCached);
        Assert.Equal("W/\"etag123\"", etag);
        Assert.NotNull(cachedReleases);
        Assert.Single(cachedReleases);
        Assert.Equal("v1.0.0", cachedReleases[0].TagName);
    }

    [Fact]
    public void CacheEntry_NonExistentRepo_ReturnsFalse()
    {
        var cache = new ETagReleaseCache();
        var hasCached = cache.TryGet("unknown", "repo", out var etag, out var cachedReleases);

        Assert.False(hasCached);
        Assert.Null(etag);
        Assert.Null(cachedReleases);
    }
}
