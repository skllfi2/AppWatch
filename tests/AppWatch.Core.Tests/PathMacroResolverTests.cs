using AppWatch.Core.Domain;
using Xunit;

namespace AppWatch.Core.Tests;

public class PathMacroResolverTests
{
    [Fact]
    public void ContractMacro_AppsRoot_ReplacesWithMacro()
    {
        var appsDir = AppPaths.DefaultInstallRoot;
        var fullPath = Path.Combine(appsDir, "ripgrep");

        var contracted = PathMacroResolver.Contract(fullPath);

        Assert.Equal(@"%APPS%\ripgrep", contracted);
    }

    [Fact]
    public void ExpandMacro_AppsRoot_RestoresFullPath()
    {
        var macroPath = @"%APPS%\ripgrep";
        var expanded = PathMacroResolver.Expand(macroPath);

        var expected = Path.Combine(AppPaths.DefaultInstallRoot, "ripgrep");
        Assert.Equal(expected, expanded);
    }

    [Fact]
    public void ContractMacro_UserProfile_ReplacesWithMacro()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var fullPath = Path.Combine(userProfile, "Tools", "bin");

        var contracted = PathMacroResolver.Contract(fullPath);

        Assert.Equal(@"%USERPROFILE%\Tools\bin", contracted);
    }

    [Fact]
    public void ExpandMacro_UserProfile_RestoresFullPath()
    {
        var macroPath = @"%USERPROFILE%\Tools\bin";
        var expanded = PathMacroResolver.Expand(macroPath);

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var expected = Path.Combine(userProfile, "Tools", "bin");
        Assert.Equal(expected, expanded);
    }

    [Fact]
    public void ExpandMacro_NullOrEmpty_ReturnsOriginal()
    {
        Assert.Null(PathMacroResolver.Expand(null));
        Assert.Equal("", PathMacroResolver.Expand(""));
        Assert.Equal("   ", PathMacroResolver.Expand("   "));
    }

    [Fact]
    public void ContractMacro_NullOrEmpty_ReturnsOriginal()
    {
        Assert.Null(PathMacroResolver.Contract(null));
        Assert.Equal("", PathMacroResolver.Contract(""));
        Assert.Equal("   ", PathMacroResolver.Contract("   "));
    }
}
