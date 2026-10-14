using AppWatch.Core.Domain;

namespace AppWatch.Core.Tests;

public class DownloadMirrorsTests
{
    private const string AssetUrl =
        "https://github.com/sharkdp/bat/releases/download/v0.26.1/bat-v0.26.1-x86_64-pc-windows-msvc.zip";

    [Fact]
    public void Candidates_OriginalFirst_ThenMirrorsInOrder()
    {
        var candidates = DownloadMirrors.Candidates(
            AssetUrl, ["https://ghfast.top/", "https://ghproxy.net/"]);

        Assert.Equal(3, candidates.Count);
        Assert.Equal(AssetUrl, candidates[0]);
        Assert.Equal("https://ghfast.top/" + AssetUrl, candidates[1]);
        Assert.Equal("https://ghproxy.net/" + AssetUrl, candidates[2]);
    }

    [Fact]
    public void Candidates_TrailingSlashNormalized_NoDuplicateSlash()
    {
        var candidates = DownloadMirrors.Candidates(
            AssetUrl, ["https://ghfast.top"]);

        Assert.Equal("https://ghfast.top/" + AssetUrl, candidates[1]);
        Assert.DoesNotContain("https://ghfast.top//" + AssetUrl, candidates);
    }

    [Fact]
    public void Candidates_NullMirrors_SingleCandidate()
    {
        var candidates = DownloadMirrors.Candidates(AssetUrl, null);

        Assert.Single(candidates);
        Assert.Equal(AssetUrl, candidates[0]);
    }

    [Fact]
    public void Candidates_EmptyAndWhitespacePrefixesSkipped()
    {
        var candidates = DownloadMirrors.Candidates(
            AssetUrl, ["", "   ", "https://ghfast.top/"]);

        Assert.Equal(2, candidates.Count);
        Assert.Equal("https://ghfast.top/" + AssetUrl, candidates[1]);
    }

    [Fact]
    public void Candidates_ApiUrl_NotMirrored()
    {
        var candidates = DownloadMirrors.Candidates(
            "https://api.github.com/repos/sharkdp/bat/releases?per_page=30",
            ["https://ghfast.top/"]);

        Assert.Single(candidates);
    }

    [Fact]
    public void Candidates_NonGithubHost_NotMirrored()
    {
        var candidates = DownloadMirrors.Candidates(
            "https://example.com/file.zip", ["https://ghfast.top/"]);

        Assert.Single(candidates);
    }

    [Fact]
    public void Candidates_GithubNonReleaseUrl_NotMirrored()
    {
        var candidates = DownloadMirrors.Candidates(
            "https://github.com/sharkdp/bat", ["https://ghfast.top/"]);

        Assert.Single(candidates);
    }

    [Fact]
    public void Candidates_DuplicatePrefix_Deduplicated()
    {
        var candidates = DownloadMirrors.Candidates(
            AssetUrl, ["https://ghfast.top/", "https://ghfast.top", "HTTPS://GHFast.top/"]);

        Assert.Equal(2, candidates.Count); // original + one mirror (case-insensitive dedup)
    }

    [Fact]
    public void IsMirrorable_ReleaseDownload_True()
    {
        Assert.True(DownloadMirrors.IsMirrorable(AssetUrl));
    }

    [Fact]
    public void IsMirrorable_Api_False()
    {
        Assert.False(DownloadMirrors.IsMirrorable("https://api.github.com/repos/x/y/releases"));
    }
}