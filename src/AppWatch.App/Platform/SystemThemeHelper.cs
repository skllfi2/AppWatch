namespace AppWatch.Platform;

/// <summary>
/// Shared helper for system theme detection.
/// Ported from ZuiV2 Zui.Core/Services/SystemThemeHelper.cs (our own code).
/// </summary>
public static class SystemThemeHelper
{
    /// <summary>Returns true if the system is using a dark theme.</summary>
    public static bool IsSystemDarkTheme()
    {
        try
        {
            var uiSettings = new Windows.UI.ViewManagement.UISettings();
            var background = uiSettings.GetColorValue(Windows.UI.ViewManagement.UIColorType.Background);
            return background.R < 128;
        }
        catch
        {
            return true; // default to dark on error
        }
    }
}
