using AppWatch.Core.Domain;
using AppWatch.Core.Domain.Models;
using Xunit;

namespace AppWatch.Core.Tests;

/// <summary>
/// StarsImportHeuristics: given release asset file names, detect UpdateKind and exe name.
/// </summary>
public class StarsImportHeuristicsTests
{
    [Theory]
    [InlineData(new[] { "app-v1.0-win64.zip" }, UpdateKind.PortableZip)]
    [InlineData(new[] { "app-windows-x64.zip" }, UpdateKind.PortableZip)]
    [InlineData(new[] { "app-v1.0-win.7z" }, UpdateKind.PortableZip)]
    [InlineData(new[] { "app-win64.exe" }, UpdateKind.PortableExe)]
    [InlineData(new[] { "app.exe" }, UpdateKind.PortableExe)]
    [InlineData(new[] { "setup.exe" }, UpdateKind.Installer)]
    [InlineData(new[] { "installer.msi" }, UpdateKind.Installer)]
    [InlineData(new[] { "setup_windows.exe" }, UpdateKind.Installer)]
    public void SuggestUpdateKind_CommonPatterns(string[] assets, UpdateKind expected)
    {
        var kind = StarsImportHeuristics.SuggestUpdateKind(assets);
        Assert.Equal(expected, kind);
    }

    [Fact]
    public void SuggestUpdateKind_NoWindowsAssets_ReturnsNull()
    {
        var assets = new[] { "app.dmg", "app.deb", "app.apk" };
        var kind = StarsImportHeuristics.SuggestUpdateKind(assets);
        Assert.Null(kind);
    }

    [Fact]
    public void SuggestUpdateKind_EmptyList_ReturnsNull()
    {
        Assert.Null(StarsImportHeuristics.SuggestUpdateKind([]));
    }

    [Fact]
    public void SuggestUpdateKind_PortableZipOverExe()
    {
        var assets = new[] { "app.exe", "app-win64.zip" };
        var kind = StarsImportHeuristics.SuggestUpdateKind(assets);
        Assert.Equal(UpdateKind.PortableZip, kind);
    }

    [Fact]
    public void SuggestExecutableName_PortableExe_StripsSuffixes()
    {
        var assets = new[] { "MyApp-v1.2.3-win64.exe", "MyApp.exe" };
        var name = StarsImportHeuristics.SuggestExecutableName(assets, UpdateKind.PortableExe);
        Assert.Equal("MyApp.exe", name);
    }

    [Fact]
    public void SuggestExecutableName_StripsVersionArchSuffixes()
    {
        var assets = new[] { "OptimizerDuck-v0.5.9.0-win64.exe" };
        var name = StarsImportHeuristics.SuggestExecutableName(assets, UpdateKind.PortableExe);
        Assert.Equal("OptimizerDuck.exe", name);
    }

    [Fact]
    public void IsWindowsAsset_ExcludesNonWindows()
    {
        Assert.False(StarsImportHeuristics.IsWindowsAsset("app.dmg"));
        Assert.False(StarsImportHeuristics.IsWindowsAsset("app.AppImage"));
        Assert.False(StarsImportHeuristics.IsWindowsAsset("app.deb"));
        Assert.False(StarsImportHeuristics.IsWindowsAsset("app.apk"));
    }

    [Fact]
    public void IsWindowsAsset_DetectsWindows()
    {
        Assert.True(StarsImportHeuristics.IsWindowsAsset("app-win64.zip"));
        Assert.True(StarsImportHeuristics.IsWindowsAsset("app-windows.exe"));
        Assert.True(StarsImportHeuristics.IsWindowsAsset("app.exe"));
        Assert.True(StarsImportHeuristics.IsWindowsAsset("app.zip")); // cross-platform archive
    }

    [Fact]
    public void SuggestUpdateKind_NoAssets_Null()
    {
        Assert.Null(StarsImportHeuristics.SuggestUpdateKind([]));
    }

    [Fact]
    public void SuggestExecutableName_NotForInstaller()
    {
        var name = StarsImportHeuristics.SuggestExecutableName(new[] { "setup.exe" }, UpdateKind.Installer);
        Assert.Null(name);
    }

    [Fact]
    public void IsWindowsAsset_FiltersNonWindowsMarkers()
    {
        Assert.False(StarsImportHeuristics.IsWindowsAsset("app-mac.dmg"));
        Assert.False(StarsImportHeuristics.IsWindowsAsset("app-linux.AppImage"));
        Assert.False(StarsImportHeuristics.IsWindowsAsset("app.apk"));
        Assert.False(StarsImportHeuristics.IsWindowsAsset("app-ios.ipa"));
    }
}