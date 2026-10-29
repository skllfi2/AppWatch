using AppWatch.Core.Abstractions;
using AppWatch.Core.Domain.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace AppWatch.Platform;

/// <summary>
/// Manages the application theme (system/dark/light): applies ElementTheme to the
/// window root and swaps App-level resource dictionaries.
/// Ported from ZuiV2 Services/ThemeService.cs (our own code); reads persisted theme from IConfigStore.
/// </summary>
public sealed class ThemeService
{
    private readonly IConfigStore _configStore;
    private AppTheme _desired;
    private AppTheme _resolved;

    public AppTheme CurrentTheme => _resolved;
    public AppTheme DesiredTheme => _desired;
    public event EventHandler<AppTheme>? ThemeChanged;

    public ThemeService(IConfigStore configStore)
    {
        _configStore = configStore;
        var cfg = configStore.Load();
        _desired = cfg.Theme; // AppConfig now has Theme property
        _resolved = ResolveTheme(_desired);
    }

    public void SetTheme(AppTheme theme)
    {
        _desired = theme;
        _resolved = ResolveTheme(theme);

        var cfg = _configStore.Load();
        cfg.Theme = theme;
        _configStore.Save(cfg);

        ApplyToRoot();
        ThemeChanged?.Invoke(this, _resolved);
    }

    /// <summary>
    /// Applies the resolved theme to the window root (RequestedTheme) and swaps
    /// App-level resource dictionaries. Safe to call before the window is created — no-op.
    /// </summary>
    public void ApplyToRoot()
    {
        if (App.Window?.Content is FrameworkElement root)
        {
            root.RequestedTheme = _resolved switch
            {
                AppTheme.Light => ElementTheme.Light,
                _ => ElementTheme.Dark,
            };
        }

        ApplyThemeToResources(_resolved);
    }

    private static AppTheme ResolveTheme(AppTheme desired)
    {
        if (desired != AppTheme.System) return desired;
        return SystemThemeHelper.IsSystemDarkTheme() ? AppTheme.Dark : AppTheme.Light;
    }

    private static void ApplyThemeToResources(AppTheme resolved)
    {
        var dictionaries = Application.Current.Resources.MergedDictionaries;
        // Remove old theme and design system dictionaries (keep XamlControlsResources at index 0)
        for (var i = dictionaries.Count - 1; i >= 1; i--)
            dictionaries.RemoveAt(i);
        // Add unified theme resources, then design system (so design system styles can reference theme brushes)
        dictionaries.Add(new ResourceDictionary { Source = new Uri("ms-appx:///Themes/ThemeResources.xaml") });
        dictionaries.Add(new ResourceDictionary { Source = new Uri("ms-appx:///Styles/DesignSystem.xaml") });
    }
}
