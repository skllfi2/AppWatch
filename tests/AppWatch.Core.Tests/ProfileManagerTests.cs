using System.Text.Json;
using System.Text.Json.Serialization;
using AppWatch.Core.Domain;
using AppWatch.Core.Domain.Models;
using Xunit;

namespace AppWatch.Core.Tests;

public class ProfileManagerTests
{
    [Fact]
    public void ExportManifest_RedactsSecrets_AndContractsPaths()
    {
        var appsDir = AppPaths.DefaultInstallRoot;
        var config = new AppConfig
        {
            GitHubToken = "ghp_super_secret_token_12345",
            Language = "ru",
            TrackedApps =
            [
                new TrackedApp
                {
                    Name = "Ripgrep",
                    RepoOwner = "BurntSushi",
                    RepoName = "ripgrep",
                    InstallDirectory = Path.Combine(appsDir, "ripgrep"),
                    ExecutableName = "rg.exe",
                    Category = AppCategory.DevToolsAndCli,
                    SourceOrigin = AppSourceOrigin.GitHubRelease,
                    Tags = ["cli", "search"]
                }
            ]
        };

        var manifestJson = ProfileManager.ExportManifestJson(config);
        var manifest = ProfileManager.ParseManifestJson(manifestJson);

        Assert.NotNull(manifest);
        Assert.Null(manifest.GitHubToken); // Token MUST be stripped/redacted in portable manifest
        Assert.Single(manifest.TrackedApps);

        var app = manifest.TrackedApps[0];
        Assert.Equal("Ripgrep", app.Name);
        Assert.Equal(@"%APPS%\ripgrep", app.InstallDirectory); // Path contracted with macro
        Assert.Equal(AppCategory.DevToolsAndCli, app.Category);
        Assert.Equal(["cli", "search"], app.Tags);
    }

    [Fact]
    public void ImportManifest_ExpandsPaths_UnderCurrentMachineRoot()
    {
        var manifest = new ProfileManifest
        {
            TrackedApps =
            [
                new TrackedAppDto
                {
                    Name = "Ripgrep",
                    RepoOwner = "BurntSushi",
                    RepoName = "ripgrep",
                    InstallDirectory = @"%APPS%\ripgrep",
                    ExecutableName = "rg.exe",
                    Category = AppCategory.DevToolsAndCli,
                    SourceOrigin = AppSourceOrigin.GitHubRelease,
                    Tags = ["cli"]
                }
            ]
        };

        var targetConfig = new AppConfig();
        var report = ProfileManager.ApplyManifest(manifest, targetConfig);

        Assert.Equal(1, report.AddedCount);
        Assert.Single(targetConfig.TrackedApps);

        var app = targetConfig.TrackedApps[0];
        var expectedPath = Path.Combine(AppPaths.DefaultInstallRoot, "ripgrep");
        Assert.Equal(expectedPath, app.InstallDirectory);
        Assert.Equal("rg.exe", app.ExecutableName);
    }

    [Fact]
    public void ImportManifest_DeduplicatesExistingApps_ByRepo()
    {
        var existing = new TrackedApp
        {
            Name = "Existing Ripgrep",
            RepoOwner = "BurntSushi",
            RepoName = "ripgrep"
        };
        var targetConfig = new AppConfig { TrackedApps = [existing] };

        var manifest = new ProfileManifest
        {
            TrackedApps =
            [
                new TrackedAppDto
                {
                    Name = "Duplicate Ripgrep",
                    RepoOwner = "BurntSushi",
                    RepoName = "ripgrep",
                    ExecutableName = "rg.exe"
                }
            ]
        };

        var report = ProfileManager.ApplyManifest(manifest, targetConfig);

        Assert.Equal(0, report.AddedCount);
        Assert.Equal(1, report.SkippedDuplicatesCount);
        Assert.Single(targetConfig.TrackedApps);
        Assert.Equal("Existing Ripgrep", targetConfig.TrackedApps[0].Name);
    }
}
