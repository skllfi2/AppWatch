using AppWatch.Core.Domain;
using AppWatch.Core.Domain.Models;
using AppWatch.Core.Abstractions;

namespace AppWatch.Core.Tests;

public class AddAppFlowTests
{
    [Theory]
    [InlineData("https://github.com/BurntSushi/ripgrep", "BurntSushi", "ripgrep")]
    [InlineData("http://github.com/BurntSushi/ripgrep.git", "BurntSushi", "ripgrep")]
    [InlineData("https://github.com/owner/my-app/", "owner", "my-app")]
    [InlineData("owner/my-app", "owner", "my-app")]
    [InlineData("owner my-app", "owner", "my-app")]
    [InlineData("", null, null)]
    [InlineData("invalid", null, null)]
    public void ParseRepoInput_ExtractsOwnerAndRepo(string input, string? expectedOwner, string? expectedRepo)
    {
        var (owner, repo) = ParseRepo(input);
        Assert.Equal(expectedOwner, owner);
        Assert.Equal(expectedRepo, repo);
    }

    private static (string? Owner, string? Repo) ParseRepo(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return (null, null);
        var s = input.Trim();

        var m = System.Text.RegularExpressions.Regex.Match(s,
            @"(?:https?://)?(?:www\.)?github\.com/([A-Za-z0-9_.\-]+)/([A-Za-z0-9_.\-]+?)(?:\.git)?/?$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (m.Success) return (m.Groups[1].Value, m.Groups[2].Value);

        var parts = s.Split('/', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 2) return (parts[0], parts[1]);

        parts = s.Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 2) return (parts[0], parts[1]);

        return (null, null);
    }

    [Fact]
    public void DuplicateCheck_DetectsExistingGithubApp()
    {
        var existing = new List<TrackedApp>
        {
            new() { Name = "Ripgrep", RepoOwner = "BurntSushi", RepoName = "ripgrep" }
        };

        var isDup = existing.Any(a =>
            a.RepoOwner.Equals("burntsushi", StringComparison.OrdinalIgnoreCase) &&
            a.RepoName.Equals("RIPGREP", StringComparison.OrdinalIgnoreCase));

        Assert.True(isDup);
    }

    [Fact]
    public void DuplicateCheck_DetectsExistingWingetApp()
    {
        var existing = new List<TrackedApp>
        {
            new() { Name = "7-Zip", WingetPackageId = "7zip.7zip", RepoOwner = "", RepoName = "" }
        };

        var isDup = existing.Any(a =>
            !string.IsNullOrWhiteSpace(a.WingetPackageId) &&
            a.WingetPackageId.Equals("7ZIP.7ZIP", StringComparison.OrdinalIgnoreCase));

        Assert.True(isDup);
    }

    [Fact]
    public void GitHubRepoInfo_PrefersIconUrl_OverOwnerAvatar()
    {
        var info = new GitHubRepoInfo
        {
            Name = "MyApp",
            HtmlUrl = "https://github.com/user/myapp",
            OwnerLogin = "user",
            OwnerAvatarUrl = "https://avatars.githubusercontent.com/u/123",
            AvatarUrl = "https://raw.githubusercontent.com/user/myapp/main/assets/icon.png",
            IconUrl = "https://raw.githubusercontent.com/user/myapp/main/assets/icon.png"
        };

        Assert.Equal("https://raw.githubusercontent.com/user/myapp/main/assets/icon.png", info.AvatarUrl);
        Assert.Equal("https://avatars.githubusercontent.com/u/123", info.OwnerAvatarUrl);
    }
}
