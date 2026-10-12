using AppWatch.Core.Abstractions;
using AppWatch.Core.Domain.Models;
using AppWatch.Infrastructure;
using AppWatch.Localization;
using AppWatch.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AppWatch.ViewModels;

/// <summary>
/// Settings page: language, theme, auto-check interval, GitHub PAT, self-update.
/// Reads/writes AppConfig through IConfigStore; theme applies immediately via ThemeService.
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly IConfigStore _configStore;
    private readonly AppConfig _config;
    private readonly ThemeService _theme;
    private readonly SelfUpdateService _selfUpdate;
    private bool _initialized;

    public SettingsViewModel(IConfigStore configStore, ThemeService theme, SelfUpdateService selfUpdate)
    {
        _configStore = configStore;
        _config = configStore.Load();
        _theme = theme;
        _selfUpdate = selfUpdate;

        // Assignments below must NOT trigger side effects (theme swap /
        // language switch) — the page is not loaded yet and a mid-navigation
        // ResourceDictionary swap crashes StaticResource resolution.
        SelectedLanguageIndex = _config.Language == "ru" ? 0 : 1;
        SelectedThemeIndex = (int)_theme.DesiredTheme;
        AutoCheckEnabled = _config.AutoCheckEnabled;
        SelectedIntervalIndex = IntervalChoices.ToList().IndexOf(_config.CheckIntervalHours);
        if (SelectedIntervalIndex < 0) SelectedIntervalIndex = IntervalChoices.ToList().IndexOf(6);

        _initialized = true;
    }

    public IReadOnlyList<int> IntervalChoices { get; } = [1, 3, 6, 12, 24];

    [ObservableProperty]
    public partial int SelectedLanguageIndex { get; set; }

    [ObservableProperty]
    public partial int SelectedThemeIndex { get; set; }

    [ObservableProperty]
    public partial bool AutoCheckEnabled { get; set; }

    [ObservableProperty]
    public partial int SelectedIntervalIndex { get; set; }

    [ObservableProperty]
    public partial string PatToken { get; set; } = "";

    [ObservableProperty]
    public partial string StatusText { get; set; } = "";

    [ObservableProperty]
    public partial bool SelfUpdateBusy { get; set; }

    partial void OnSelectedThemeIndexChanged(int value)
    {
        if (!_initialized) return;
        _theme.SetTheme((AppTheme)value);
    }

    partial void OnSelectedLanguageIndexChanged(int value)
    {
        if (!_initialized) return;
        var lang = value == 0 ? "ru" : "en";
        LocalizationService.Instance.SetLanguage(lang);
    }

    public string PatStatus => string.IsNullOrEmpty(_config.GitHubToken)
        ? LocalizationService.Instance["SettingsPatStatusEmpty"]
        : LocalizationService.Instance["SettingsPatStatusOk"];

    [RelayCommand]
    private void Save()
    {
        _config.Language = SelectedLanguageIndex == 0 ? "ru" : "en";
        _config.AutoCheckEnabled = AutoCheckEnabled;
        _config.CheckIntervalHours = IntervalChoices[Math.Max(0, SelectedIntervalIndex)];
        if (!string.IsNullOrWhiteSpace(PatToken))
            _config.GitHubToken = PatToken.Trim();
        _configStore.Save(_config);
        StatusText = LocalizationService.Instance["SettingsSaved"];
        OnPropertyChanged(nameof(PatStatus));
    }

    [RelayCommand]
    private async Task CheckSelfUpdateAsync(CancellationToken cancellationToken)
    {
        SelfUpdateBusy = true;
        StatusText = "";
        try
        {
            var version = typeof(SettingsViewModel).Assembly.GetName().Version?.ToString(3) ?? "0.1.0";
            var prepared = await _selfUpdate.PrepareAsync(version, cancellationToken);
            StatusText = prepared is null
                ? LocalizationService.Instance["SettingsNoUpdates"]
                : string.Format(LocalizationService.Instance["SettingsUpdatesFound"], prepared.Version);
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
        }
        finally
        {
            SelfUpdateBusy = false;
        }
    }
}
