using AppWatch.Core.Domain;
using Xunit;

namespace AppWatch.Core.Tests;

public class AppPathsTests
{
    [Fact]
    public void DataRoot_IsAppWatchDataUnderLocalAppData()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var expected = Path.Combine(localAppData, "AppWatchData");
        Assert.Equal(expected, AppPaths.DataRoot);
    }

    [Fact]
    public void DefaultInstallRoot_IsAppsUnderDataRoot()
    {
        var expected = Path.Combine(AppPaths.DataRoot, "Apps");
        Assert.Equal(expected, AppPaths.DefaultInstallRoot);
    }

    [Fact]
    public void DefaultVersionsRoot_IsVersionsUnderDataRoot()
    {
        var expected = Path.Combine(AppPaths.DataRoot, "Versions");
        Assert.Equal(expected, AppPaths.DefaultVersionsRoot);
    }

    [Fact]
    public void DefaultLogsRoot_IsLogsUnderDataRoot()
    {
        var expected = Path.Combine(AppPaths.DataRoot, "logs");
        Assert.Equal(expected, AppPaths.DefaultLogsRoot);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ResolveInstallRoot_WhenNullOrWhiteSpace_ReturnsDefaultInstallRoot(string? configured)
    {
        var resolved = AppPaths.ResolveInstallRoot(configured);
        Assert.Equal(AppPaths.DefaultInstallRoot, resolved);
    }

    [Fact]
    public void ResolveInstallRoot_WhenCustomPath_ReturnsCustomPath()
    {
        var custom = @"D:\MyApps";
        var resolved = AppPaths.ResolveInstallRoot(custom);
        Assert.Equal(custom, resolved);
    }

    [Fact]
    public void ResolveInstallRoot_WhenLegacyPath_RedirectsToDefaultInstallRoot()
    {
        var resolved = AppPaths.ResolveInstallRoot(AppPaths.LegacyInstallRoot);
        Assert.Equal(AppPaths.DefaultInstallRoot, resolved);
    }

    [Fact]
    public void PortableMode_WhenFlagPresent_RedirectsRootsToAppDirectory()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "AppWatchPortableTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "portable.flag"), "");

            AppPaths.InitializePortableMode(tempDir);

            Assert.True(AppPaths.IsPortableMode);
            Assert.Equal(Path.Combine(tempDir, "Data"), AppPaths.DataRoot);
            Assert.Equal(Path.Combine(tempDir, "Data", "Apps"), AppPaths.DefaultInstallRoot);
            Assert.Equal(Path.Combine(tempDir, "Data", "Config"), AppPaths.ConfigDirectory);
        }
        finally
        {
            AppPaths.ResetPortableMode();
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }
}
