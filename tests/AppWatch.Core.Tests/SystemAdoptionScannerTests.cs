using AppWatch.Core.Domain;
using AppWatch.Core.Domain.Models;
using Xunit;

namespace AppWatch.Core.Tests;

public class SystemAdoptionScannerTests
{
    [Fact]
    public void MatchAgainstCatalog_MatchesExactExecutableName()
    {
        var catalog = new List<CatalogEntry>
        {
            new()
            {
                Name = "Ripgrep",
                RepoOwner = "BurntSushi",
                RepoName = "ripgrep",
                ExecutableName = "rg.exe",
                UpdateKind = UpdateKind.PortableZip,
                Category = "DevToolsAndCli"
            },
            new()
            {
                Name = "Lazygit",
                RepoOwner = "jesseduffield",
                RepoName = "lazygit",
                ExecutableName = "lazygit.exe",
                UpdateKind = UpdateKind.PortableZip,
                Category = "DevToolsAndCli"
            }
        };

        var candidate = new DiscoveredBinary(
            FilePath: @"C:\bin\rg.exe",
            FileName: "rg.exe",
            ProductName: "ripgrep",
            FileVersion: "14.1.0",
            Source: AdoptionSource.PathEnvironment
        );

        var match = SystemAdoptionScanner.TryMatch(candidate, catalog);

        Assert.NotNull(match);
        Assert.Equal("BurntSushi", match.RepoOwner);
        Assert.Equal("ripgrep", match.RepoName);
        Assert.Equal("rg.exe", match.ExecutableName);
    }

    [Fact]
    public void MatchAgainstCatalog_MatchesByProductName_WhenExecutableDiffers()
    {
        var catalog = new List<CatalogEntry>
        {
            new()
            {
                Name = "yt-dlp",
                RepoOwner = "yt-dlp",
                RepoName = "yt-dlp",
                ExecutableName = "yt-dlp.exe",
                UpdateKind = UpdateKind.PortableExe,
                Category = "MediaAndGraphics"
            }
        };

        var candidate = new DiscoveredBinary(
            FilePath: @"D:\Tools\youtube-downloader.exe",
            FileName: "youtube-downloader.exe",
            ProductName: "yt-dlp",
            FileVersion: "2024.10.07",
            Source: AdoptionSource.CustomDirectory
        );

        var match = SystemAdoptionScanner.TryMatch(candidate, catalog);

        Assert.NotNull(match);
        Assert.Equal("yt-dlp", match.RepoOwner);
        Assert.Equal("yt-dlp", match.RepoName);
    }

    [Fact]
    public void BuildTrackedApp_InPlace_SetsIsInPlaceInstallTrue()
    {
        var candidate = new DiscoveredBinary(
            FilePath: @"D:\MyTools\rg.exe",
            FileName: "rg.exe",
            ProductName: "ripgrep",
            FileVersion: "14.1.0",
            Source: AdoptionSource.CustomDirectory
        );

        var catalogMatch = new CatalogEntry
        {
            Name = "Ripgrep",
            RepoOwner = "BurntSushi",
            RepoName = "ripgrep",
            ExecutableName = "rg.exe",
            UpdateKind = UpdateKind.PortableZip
        };

        var app = SystemAdoptionScanner.CreateInPlaceTrackedApp(candidate, catalogMatch);

        Assert.Equal("Ripgrep", app.Name);
        Assert.Equal("BurntSushi", app.RepoOwner);
        Assert.Equal("ripgrep", app.RepoName);
        Assert.Equal(@"D:\MyTools", app.InstallDirectory);
        Assert.Equal("14.1.0", app.CurrentVersion);
        Assert.True(app.IsInPlaceInstall);
        Assert.Equal(AppSourceOrigin.LocalInPlace, app.SourceOrigin);
    }
}
