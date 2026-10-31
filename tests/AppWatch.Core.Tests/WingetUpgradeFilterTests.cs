using AppWatch.Core.Domain;

namespace AppWatch.Core.Tests;

public class WingetUpgradeFilterTests
{
    private static WingetPackageUpdate Row(
        string name = "App", string id = "Pub.App",
        string current = "1.0.0", string available = "2.0.0") =>
        new(name, id, current, available);

    [Fact]
    public void SystemVersionNewerOrEqual_HidesRow()
    {
        var rows = new[] { Row(name: "Battle.net", id: "Blizzard.BattleNet", current: "Unknown", available: "1.19.3.3219") };

        var result = WingetUpgradeFilter.Filter(rows, name =>
            name == "Battle.net" ? "1.19.3.3219" : null);

        Assert.Empty(result.Visible);
        Assert.Single(result.Hidden);
        Assert.Equal("1.19.3.3219", result.Hidden[0].SystemVersion);
    }

    [Fact]
    public void SystemVersionOlder_ShowsRowWithSystemVersion()
    {
        var rows = new[] { Row(current: "Unknown", available: "2.0.0") };

        var result = WingetUpgradeFilter.Filter(rows, name => name == "App" ? "1.5.0" : null);

        Assert.Single(result.Visible);
        Assert.Equal("1.5.0", result.Visible[0].SystemVersion);
    }

    [Fact]
    public void UnresolvableUnknownRow_StaysVisibleWithNullSystemVersion()
    {
        // cannot determine the installed version at all → behave like winget (offer the update)
        var rows = new[] { Row(current: "Unknown", available: "2.0.0") };

        var result = WingetUpgradeFilter.Filter(rows, _ => null);

        Assert.Single(result.Visible);
        Assert.Null(result.Visible[0].SystemVersion);
    }

    [Fact]
    public void KnownWingetVersion_IsKeptAsIs()
    {
        var rows = new[] { Row(current: "1.29.379.0", available: "1.29.380") };

        var result = WingetUpgradeFilter.Filter(rows, _ => null);

        Assert.Single(result.Visible);
        Assert.Null(result.Visible[0].SystemVersion);
    }

    [Fact]
    public void SystemVersionUnparsable_TreatedAsUnknown()
    {
        // e.g. VersionInfo returns garbage like "Battle.net Setup" — must not crash, must keep the row
        var rows = new[] { Row(current: "Unknown", available: "2.0.0") };

        var result = WingetUpgradeFilter.Filter(rows, name => name == "App" ? "not a version" : null);

        Assert.Single(result.Visible);
    }

    [Fact]
    public void MultipleRows_MixedOutcomes()
    {
        var rows = new[]
        {
            Row(name: "A", id: "Pub.A", current: "Unknown", available: "3.0.0"),
            Row(name: "B", id: "Pub.B", current: "1.0.0", available: "9.9.9"),
            Row(name: "C", id: "Pub.C", current: "Unknown", available: "1.0.0"),
        };

        var result = WingetUpgradeFilter.Filter(rows,
            name => name switch { "A" => "4.0.0", "C" => "1.0.0", _ => null });

        // A: system 4.0.0 >= 3.0.0 → hidden; B: no system version → visible; C: system 1.0.0 == available → hidden
        Assert.Single(result.Visible);
        Assert.Equal("Pub.B", result.Visible[0].Update.Id);
        Assert.Equal(2, result.Hidden.Count);
        Assert.Contains(result.Hidden, r => r.Update.Id == "Pub.A");
        Assert.Contains(result.Hidden, r => r.Update.Id == "Pub.C");
    }

    [Theory]
    [InlineData("Xiaomi MiMo AI 26.1023.231655", "26.1023.231655", null, "Xiaomi MiMo AI")]
    [InlineData("Xiaomi MiMo AI 26.1023.231655", "Unknown", null, "Xiaomi MiMo AI")]
    [InlineData("Xiaomi MiMo AI 26.1023.231655", null, null, "Xiaomi MiMo AI")]
    [InlineData("Node.js v20.10.0", "20.10.0", null, "Node.js")]
    [InlineData("Node.js v20.10.0", "Unknown", null, "Node.js")]
    [InlineData("App (1.2.3)", "1.2.3", null, "App")]
    [InlineData("App v.1.2.3", "1.2.3", null, "App")]
    [InlineData("Visual Studio Build Tools 2022", "17.14.42", null, "Visual Studio Build Tools 2022")]
    [InlineData("Visual Studio Community 2026", "18.10.3", null, "Visual Studio Community 2026")]
    [InlineData("7-Zip 23.01", "23.01", null, "7-Zip")]
    [InlineData("7-Zip 23.01", null, null, "7-Zip")]
    [InlineData("Python 3.12.3", "Unknown", null, "Python")]
    [InlineData("Git", "2.45.1", null, "Git")]
    public void CleanName_StripsTrailingVersionAppropriately(
        string rawName, string? currentVer, string? systemVer, string expected)
    {
        var cleaned = WingetUpgradeRow.CleanName(rawName, currentVer, systemVer);
        Assert.Equal(expected, cleaned);
    }
}
