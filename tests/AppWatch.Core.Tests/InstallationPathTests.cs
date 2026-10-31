using AppWatch.Core.Domain;
using AppWatch.Core.Domain.Models;
using Xunit;

namespace AppWatch.Core.Tests;

public class InstallationPathTests
{
    [Fact]
    public void CatalogEntry_ToTrackedApp_AllowsNullInstallDirectory_ForInstaller()
    {
        var entry = new CatalogEntry
        {
            Name = "Git for Windows",
            RepoOwner = "git-for-windows",
            RepoName = "git",
            UpdateKind = UpdateKind.Installer,
            Description = "Fast, scalable, distributed revision control system",
        };

        var app = entry.ToTrackedApp(installDirectory: null);

        Assert.Equal("Git for Windows", app.Name);
        Assert.Equal("git-for-windows", app.RepoOwner);
        Assert.Equal("git", app.RepoName);
        Assert.Equal(UpdateKind.Installer, app.UpdateKind);
        Assert.Null(app.InstallDirectory);
        Assert.Equal("Fast, scalable, distributed revision control system", app.Description);
    }

    [Fact]
    public void CatalogEntry_ToTrackedApp_PreservesInstallDirectory_ForPortable()
    {
        var entry = new CatalogEntry
        {
            Name = "Ripgrep",
            RepoOwner = "BurntSushi",
            RepoName = "ripgrep",
            UpdateKind = UpdateKind.PortableZip,
        };

        var app = entry.ToTrackedApp(@"C:\Tools\ripgrep");

        Assert.Equal(UpdateKind.PortableZip, app.UpdateKind);
        Assert.Equal(@"C:\Tools\ripgrep", app.InstallDirectory);
    }

    [Fact]
    public void AppConfig_InstallRoot_DefaultsToNull()
    {
        var config = new AppConfig();
        Assert.Null(config.InstallRoot);
    }

    [Fact]
    public void AppConfig_InstallRoot_CanBeCustomized()
    {
        var config = new AppConfig
        {
            InstallRoot = @"D:\PortableApps"
        };
        Assert.Equal(@"D:\PortableApps", config.InstallRoot);
    }

    [Fact]
    public void UpdateDecisionEngine_EvaluatesInstallerApp_WithNullInstallDirectory()
    {
        var app = new TrackedApp
        {
            Name = "VSCode",
            RepoOwner = "microsoft",
            RepoName = "vscode",
            UpdateKind = UpdateKind.Installer,
            CurrentVersion = "1.80.0",
            InstallDirectory = null,
        };

        var release = new ReleaseInfo
        {
            TagName = "1.85.0",
            IsPreRelease = false,
            PublishedAt = DateTimeOffset.UtcNow,
            Assets = [new ReleaseAsset("VSCodeSetup-x64-1.85.0.exe", "https://example.com/vscode.exe", 95_000_000, null)]
        };

        var result = UpdateDecisionEngine.Evaluate(app, [release], SystemArchitecture.X86_64);

        Assert.Equal(UpdateStatus.UpdateAvailable, result.Status);
        Assert.NotNull(result.NewRelease);
        Assert.Equal("1.85.0", result.NewRelease!.TagName);
        Assert.NotNull(result.ChosenAsset);
        Assert.Equal("VSCodeSetup-x64-1.85.0.exe", result.ChosenAsset!.Name);
    }
}
