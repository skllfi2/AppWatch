using AppWatch.Core.Abstractions;
using AppWatch.Core.Domain.Models;
using AppWatch.Infrastructure;
using AppWatch.Localization;
using AppWatch.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Text.Json;
using System.Text.Json.Serialization;
using Windows.Storage.Pickers;

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

    public IReadOnlyList<int> IntervalChoices { get; } = [1, 3, 6, 12, 24];

    public IReadOnlyList<UpdateStrategy> StrategyChoices { get; } =
        [UpdateStrategy.Auto, UpdateStrategy.BuiltInUpdater, UpdateStrategy.SilentInstaller,
         UpdateStrategy.PortableReplace, UpdateStrategy.ExternalLauncher];

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

        SelectedUpdateStrategyIndex = StrategyChoices.ToList().IndexOf(_config.DefaultUpdateStrategy);
        if (SelectedUpdateStrategyIndex < 0) SelectedUpdateStrategyIndex = 0;

        _initialized = true;
    }

    [ObservableProperty]
    public partial int SelectedLanguageIndex { get; set; }

    [ObservableProperty]
    public partial int SelectedThemeIndex { get; set; }

    [ObservableProperty]
    public partial bool AutoCheckEnabled { get; set; }

    [ObservableProperty]
    public partial int SelectedIntervalIndex { get; set; }

    [ObservableProperty]
    public partial int SelectedUpdateStrategyIndex { get; set; }

    [ObservableProperty]
    public partial string PatToken { get; set; } = "";

    [ObservableProperty]
    public partial string StatusText { get; set; } = "";

    [ObservableProperty]
    public partial bool SelfUpdateBusy { get; set; }

    [ObservableProperty]
    public partial string NextBackgroundCheckText { get; set; } = "";

    [ObservableProperty]
    public partial string NextCatalogCheckText { get; set; } = "";

    /// <summary>
    /// C12: re-read config from disk (BackgroundMonitor/CatalogMonitor persist their
    /// next-run timestamps there) and format both schedule labels. Called on every
    /// navigation to the settings page so cached pages never show stale times.
    /// </summary>
    public void RefreshScheduleInfo()
    {
        var fresh = _configStore.Load();
        var none = LocalizationService.Instance["SettingsScheduleNone"];
        NextBackgroundCheckText = fresh.BackgroundCheckNextRunUtc is { } t
            ? t.LocalDateTime.ToString("dd.MM.yyyy HH:mm")
            : none;
        NextCatalogCheckText = fresh.CatalogCheckNextRunUtc is { } c
            ? c.LocalDateTime.ToString("dd.MM.yyyy HH:mm")
            : none;
    }

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

    partial void OnSelectedUpdateStrategyIndexChanged(int value)
    {
        if (!_initialized) return;
        _config.DefaultUpdateStrategy = StrategyChoices[Math.Max(0, value)];
        _configStore.Save(_config);
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
            
            // Store prepared update for later apply
            _preparedUpdate = prepared;
            OnPropertyChanged(nameof(CanApplySelfUpdate));
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

    private SelfUpdateService.PreparedUpdate? _preparedUpdate;

    public bool CanApplySelfUpdate => _preparedUpdate is not null && !SelfUpdateBusy;

    // === A3: export / import the app list ===

    private static readonly JsonSerializerOptions BundleOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    [RelayCommand]
    private async Task ExportAppsAsync(CancellationToken cancellationToken)
    {
        var picker = new FileSavePicker { SuggestedFileName = $"appwatch-apps-{DateTime.Now:yyyyMMdd}" };
        picker.FileTypeChoices.Add("AppWatch bundle", [".json"]);
        WinRT.Interop.InitializeWithWindow.Initialize(picker,
            WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow));
        var file = await picker.PickSaveFileAsync();
        if (file is null) return;

        await File.WriteAllTextAsync(file.Path,
            JsonSerializer.Serialize(_config.TrackedApps, BundleOptions), cancellationToken);
        StatusText = LocalizationService.Instance["ExportDone"];
    }

    [RelayCommand]
    private async Task ImportAppsAsync(CancellationToken cancellationToken)
    {
        var picker = new FileOpenPicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
        };
        picker.FileTypeFilter.Add(".json");
        WinRT.Interop.InitializeWithWindow.Initialize(picker,
            WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow));
        var file = await picker.PickSingleFileAsync();
        if (file is null) return;

        var imported = JsonSerializer.Deserialize<List<TrackedApp>>(
            await File.ReadAllTextAsync(file.Path, cancellationToken), BundleOptions) ?? [];

        var main = App.Resolve<MainViewModel>();
        var added = 0;
        foreach (var app in imported)
        {
            if (string.IsNullOrWhiteSpace(app.RepoName)) continue;
            if (_config.TrackedApps.Any(a => a.RepoOwner == app.RepoOwner && a.RepoName == app.RepoName))
                continue; // dedupe by owner/repo

            _config.TrackedApps.Add(app);
            main.Apps.Add(new TrackedAppItem(app));
            added++;
        }

        if (added > 0)
        {
            _configStore.Save(_config);
            main.AppsTrackedCount = main.Apps.Count;
        }
        StatusText = added > 0
            ? string.Format(LocalizationService.Instance["ImportDone"], added)
            : LocalizationService.Instance["ImportEmpty"];
    }

    [RelayCommand]
    private void CopyAppsList()
    {
        var lines = _config.TrackedApps.Select(a =>
            !string.IsNullOrWhiteSpace(a.WingetPackageId) ? a.WingetPackageId
            : $"https://github.com/{a.RepoOwner}/{a.RepoName}").Distinct().ToList();

        var text = string.Join(Environment.NewLine, lines);
        var package = new global::Windows.ApplicationModel.DataTransfer.DataPackage();
        package.SetText(text);
        global::Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(package);
        StatusText = string.Format(LocalizationService.Instance["CopyListDone"], lines.Count);
    }

    [RelayCommand(CanExecute = nameof(CanApplySelfUpdate))]
    private async Task ApplySelfUpdateAsync(CancellationToken cancellationToken)
    {
        if (_preparedUpdate is null) return;

        SelfUpdateBusy = true;
        StatusText = LocalizationService.Instance["SettingsSelfUpdateApplying"];
        try
        {
            var success = await _selfUpdate.ApplyAsync(_preparedUpdate!, cancellationToken);
            if (success)
            {
                StatusText = LocalizationService.Instance["SettingsSelfUpdateApplied"];
            }
            else
            {
                StatusText = LocalizationService.Instance["SettingsSelfUpdateFailed"];
            }
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
        }
        finally
        {
            SelfUpdateBusy = false;
            _preparedUpdate = null;
            OnPropertyChanged(nameof(CanApplySelfUpdate));
        }
    }
}
