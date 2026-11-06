using AppWatch.Core.Domain.Models;
using Xunit;

namespace AppWatch.Core.Tests;

public class TrackedAppsFilterTests
{
    private static TrackedApp CreateApp(string name, AppCategory category, AppSourceOrigin origin, UpdateKind kind = UpdateKind.PortableZip) =>
        new()
        {
            Name = name,
            RepoOwner = "owner",
            RepoName = name.ToLowerInvariant(),
            Category = category,
            SourceOrigin = origin,
            UpdateKind = kind
        };

    [Fact]
    public void FilterByOrigin_FiltersCorrectly()
    {
        var apps = new List<TrackedApp>
        {
            CreateApp("GitApp", AppCategory.DevToolsAndCli, AppSourceOrigin.GitHubRelease),
            CreateApp("VerseApp", AppCategory.DevToolsAndCli, AppSourceOrigin.GitVerse),
            CreateApp("LocalApp", AppCategory.SystemUtilities, AppSourceOrigin.LocalInPlace),
            CreateApp("WinApp", AppCategory.Productivity, AppSourceOrigin.WinGet, UpdateKind.WingetPackage)
        };

        var verseApps = apps.Where(a => a.SourceOrigin == AppSourceOrigin.GitVerse).ToList();
        var inPlaceApps = apps.Where(a => a.SourceOrigin == AppSourceOrigin.LocalInPlace).ToList();

        Assert.Single(verseApps);
        Assert.Equal("VerseApp", verseApps[0].Name);

        Assert.Single(inPlaceApps);
        Assert.Equal("LocalApp", inPlaceApps[0].Name);
    }

    [Fact]
    public void FilterByCategory_FiltersCorrectly()
    {
        var apps = new List<TrackedApp>
        {
            CreateApp("DevTool1", AppCategory.DevToolsAndCli, AppSourceOrigin.GitHubRelease),
            CreateApp("DevTool2", AppCategory.DevToolsAndCli, AppSourceOrigin.LocalInPlace),
            CreateApp("AiTool", AppCategory.AiAndLlm, AppSourceOrigin.GitHubRelease)
        };

        var devTools = apps.Where(a => a.Category == AppCategory.DevToolsAndCli).ToList();
        var aiTools = apps.Where(a => a.Category == AppCategory.AiAndLlm).ToList();

        Assert.Equal(2, devTools.Count);
        Assert.Single(aiTools);
        Assert.Equal("AiTool", aiTools[0].Name);
    }
}
