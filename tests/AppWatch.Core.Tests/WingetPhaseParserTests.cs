using AppWatch.Core.Domain;

namespace AppWatch.Core.Tests;

/// <summary>
/// WingetPhase detection from REAL redirected-stdout lines (captured live on the
/// user's machine — winget prints no progress bar at all when output is piped).
/// </summary>
public class WingetPhaseParserTests
{
    [Theory]
    [InlineData("Скачивание https://github.com/git-for-windows/git/releases/download/v2.55.0/Git-2.55.0.5-64-bit.exe", WingetPhase.Downloading)]
    [InlineData("Downloading https://example.com/setup.exe", WingetPhase.Downloading)]
    [InlineData("Хэш установщика успешно проверен", WingetPhase.Verifying)]
    [InlineData("Installer hash verified", WingetPhase.Verifying)]
    [InlineData("Установщик скачан: C:\\Downloads\\Git_2.55.0.5_User_X64.exe", WingetPhase.Downloaded)]
    [InlineData("Installer downloaded to: C:\\Temp\\setup.exe", WingetPhase.Downloaded)]
    [InlineData("Установка...", WingetPhase.Installing)]
    [InlineData("Installing package...", WingetPhase.Installing)]
    [InlineData("Successfully installed", WingetPhase.Installing)]
    public void TryParsePhase_RealWingetLines_MapsToPhases(string line, WingetPhase expected)
        => Assert.Equal(expected, WingetOutputParser.TryParsePhase(line));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Найдено Git [Git.Git] Версия 2.55.0.5")]
    [InlineData("Лицензия на это приложение предоставлена вам владельцем.")]
    [InlineData("В настроенном источнике доступна более новая версия пакета")]
    public void TryParsePhase_NonPhaseLines_ReturnsNull(string line)
        => Assert.Null(WingetOutputParser.TryParsePhase(line));

    [Fact]
    public void TryParsePhase_TrimsAndIgnoresCase()
        => Assert.Equal(WingetPhase.Downloading, WingetOutputParser.TryParsePhase("  СКАЧИВАНИЕ https://x/y.exe "));
}
