using AppWatch.Core.Domain;
using Xunit;

namespace AppWatch.Core.Tests;

public class WingetOutputParserTests
{
    private const string RealUpgradeOutput = """
        Name                                   Id                          Version          Available        Source
        -----------------------------------------------------------------------------------------------------------
        App Installer                          Microsoft.AppInstaller      1.29.379.0       1.29.380         winget
        Battle.net                             Blizzard.BattleNet          Unknown          1.19.3.3219      winget
        Epic Games Launcher                    EpicGames.EpicGamesLauncher 1.3.193.0        1.3.210.0        winget
        Яндекс Браузер с Алисой AI (All Users) Yandex.Browser              26.8.5.811       26.10.0.1796     winget

        7 upgrades available.
        """;

    [Fact]
    public void ParseUpgrades_RealTable_ParsesAllRows()
    {
        var result = WingetOutputParser.ParseUpgrades(RealUpgradeOutput);

        Assert.Equal(4, result.Count);
        Assert.Equal("App Installer", result[0].Name);
        Assert.Equal("Microsoft.AppInstaller", result[0].Id);
        Assert.Equal("1.29.379.0", result[0].CurrentVersion);
        Assert.Equal("1.29.380", result[0].AvailableVersion);
        Assert.Equal("Battle.net", result[1].Name);
        Assert.Equal("Unknown", result[1].CurrentVersion);
        // localized name with single spaces must survive the column split
        Assert.Equal("Яндекс Браузер с Алисой AI (All Users)", result[3].Name);
        Assert.Equal("Yandex.Browser", result[3].Id);
    }

    [Fact]
    public void ParseUpgrades_NulSeparatedOutput_ParsesCorrectly()
    {
        // winget emitting UTF-16LE decoded as UTF-8 puts NUL between every character
        var header = "Name\u0000 \u0000 \u0000Id\u0000 \u0000 \u0000Version\u0000 \u0000 \u0000Available";
        var separator = new string('-', 100) + "\u0000";
        var row = "Test\u0000 \u0000 \u0000Test.Id\u0000 \u0000 \u00001.0.0\u0000 \u0000 \u00002.0.0";

        var result = WingetOutputParser.ParseUpgrades($"{header}\n{separator}\n{row}\n");

        var entry = Assert.Single(result);
        Assert.Equal("Test", entry.Name);
        Assert.Equal("Test.Id", entry.Id);
        Assert.Equal("1.0.0", entry.CurrentVersion);
        Assert.Equal("2.0.0", entry.AvailableVersion);
    }

    [Theory]
    [InlineData("Доступные обновления отсутствуют.")]
    [InlineData("No installed package found matching input criteria.")]
    [InlineData("")]
    [InlineData("\r\n")]
    public void ParseUpgrades_NoUpdatesOrGarbage_ReturnsEmpty(string output)
    {
        Assert.Empty(WingetOutputParser.ParseUpgrades(output));
    }

    [Fact]
    public void ParseUpgrades_HeaderAndErrorLines_AreSkipped()
    {
        var output = """
            Checking for updates...
            Имя  Идентификатор  Версия  Доступно  Источник
            -----------------------------------------------------------------------------------------------------------
            App  App.Id  1.0.0  2.0.0  winget
            something went wrong with a very long error line    and extra    columns
            """;

        var result = WingetOutputParser.ParseUpgrades(output);

        Assert.Single(result);
        Assert.Equal("App", result[0].Name);
    }

    [Theory]
    [InlineData("  ██████▒▒▒▒▒▒▒  1.2 MB / 15.9 MB    45%   12s", 45)]
    [InlineData("100%", 100)]
    [InlineData("Скачивание 25 %", 25)]
    public void TryParseProgressPercent_ExtractsPercent(string line, double expected)
    {
        Assert.Equal(expected, WingetOutputParser.TryParseProgressPercent(line));
    }

    [Theory]
    [InlineData("Downloading 1.2 MB / 15.9 MB")]
    [InlineData("Установка выполнена")]
    [InlineData("")]
    [InlineData("150%")]
    public void TryParseProgressPercent_NoPercentOrOutOfRange_ReturnsNull(string line)
    {
        Assert.Null(WingetOutputParser.TryParseProgressPercent(line));
    }
}
