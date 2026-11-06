namespace AppWatch.Core.Domain.Models;

/// <summary>
/// Source origin identifying how the application was adopted into AppWatch.
/// </summary>
public enum AppSourceOrigin
{
    GitHubRelease,
    GitVerse,
    GitLab,
    WinGet,
    LocalInPlace,
    ImportedFromStars,
    CargoTool,
    NpmTool,
    PipxTool,
    Custom,
}

/// <summary>
/// Predefined application category for high-level grouping.
/// </summary>
public enum AppCategory
{
    DevToolsAndCli,
    AiAndLlm,
    SystemUtilities,
    MediaAndGraphics,
    InternetAndNetwork,
    Productivity,
    Uncategorized,
}
