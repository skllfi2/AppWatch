using AppWatch.Infrastructure.System;
using AppWatch.Core.Abstractions;
using AppWatch.Core.Domain.Models;
using AppWatch.Infrastructure;
using AppWatch.Localization;
using AppWatch.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Text.Json;
using System.Text.Json.Serialization;
using Velopack;
using Velopack.Locators;
using Velopack.Sources;
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
    private readonly VersionStore _versionStore;
    private readonly SoundService _soundService;
    private bool _initialized;

    public IReadOnlyList<int> IntervalChoices { get; } = [1, 3, 6, 12, 24];
    public IReadOnlyList<int> VersionKeepChoices { get; } = [1, 3, 5, 10];

    public IReadOnlyList<UpdateStrategy> StrategyChoices { get; } =
        [UpdateStrategy.Auto, UpdateStrategy.BuiltInUpdater, UpdateStrategy.SilentInstaller,
         UpdateStrategy.PortableReplace, UpdateStrategy.ExternalLauncher];

    public SettingsViewModel(IConfigStore configStore, ThemeService theme, SelfUpdateService selfUpdate, VersionStore versionStore, SoundService soundService)
    {
        _configStore = configStore;
        _config = configStore.Load();
        _theme = theme;
        _selfUpdate = selfUpdate;
        _versionStore = versionStore;
        _soundService = soundService;

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

        ToastNotificationsEnabled = _config.ToastNotificationsEnabled;
        SoundEffectsEnabled = _config.SoundEffectsEnabled;
        AutoRefreshIconsEnabled = _config.AutoRefreshIcons;

        PortableAppsDirectory = _config.InstallRoot
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AppWatch", "Apps");

        SelectedVersionKeepIndex = VersionKeepChoices.ToList().IndexOf(_config.VersionCacheCount);
        if (SelectedVersionKeepIndex < 0) SelectedVersionKeepIndex = 1;

        RefreshStorageSizes();

        _initialized = true;
    }

    [ObservableProperty]
    public partial int SelectedLanguageIndex { get; set; }

    [ObservableProperty]
    public partial int SelectedThemeIndex { get; set; }

    [ObservableProperty]
    public partial bool AutoCheckEnabled { get; set; }

    /// <summary>Windows app notifications for background-check findings.</summary>
    [ObservableProperty]
    public partial bool ToastNotificationsEnabled { get; set; }

    /// <summary>WinUI 3 spatial audio and UI sound effects.</summary>
    [ObservableProperty]
    public partial bool SoundEffectsEnabled { get; set; }

    [ObservableProperty]
    public partial bool AutoRefreshIconsEnabled { get; set; }

    [ObservableProperty]
    public partial bool IsRefreshingIcons { get; set; }

    [ObservableProperty]
    public partial int SelectedIntervalIndex { get; set; }

    [ObservableProperty]
    public partial int SelectedUpdateStrategyIndex { get; set; }

    [ObservableProperty]
    public partial string PortableAppsDirectory { get; set; } = "";

    [ObservableProperty]
    public partial int SelectedVersionKeepIndex { get; set; }

    [ObservableProperty]
    public partial string VersionCacheSizeText { get; set; } = "0 Б";

    [ObservableProperty]
    public partial string TempFilesSizeText { get; set; } = "0 Б";

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

        RefreshStorageSizes();
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

    partial void OnSelectedVersionKeepIndexChanged(int value)
    {
        if (!_initialized) return;
        _config.VersionCacheCount = VersionKeepChoices[Math.Clamp(value, 0, VersionKeepChoices.Count - 1)];
        _configStore.Save(_config);
    }

    public string PatStatus => string.IsNullOrEmpty(_config.GitHubToken)
        ? LocalizationService.Instance["SettingsPatStatusEmpty"]
        : LocalizationService.Instance["SettingsPatStatusOk"];

    [RelayCommand]
    private async Task BrowsePortableDirectoryAsync()
    {
        var picker = new Windows.Storage.Pickers.FolderPicker();
        picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.ComputerFolder;
        picker.FileTypeFilter.Add("*");
        WinRT.Interop.InitializeWithWindow.Initialize(picker,
            WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow));
        var folder = await picker.PickSingleFolderAsync();
        if (folder is not null)
        {
            PortableAppsDirectory = folder.Path;
            _config.InstallRoot = folder.Path;
            _configStore.Save(_config);
            StatusText = LocalizationService.Instance["SettingsSaved"];
        }
    }

    [RelayCommand]
    private void ResetPortableDirectory()
    {
        var defaultPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AppWatch", "Apps");
        PortableAppsDirectory = defaultPath;
        _config.InstallRoot = null;
        _configStore.Save(_config);
        StatusText = LocalizationService.Instance["SettingsSaved"];
    }

    partial void OnAutoRefreshIconsEnabledChanged(bool value)
    {
        if (!_initialized) return;
        _config.AutoRefreshIcons = value;
        _configStore.Save(_config);
        StatusText = LocalizationService.Instance["SettingsSaved"];
    }

    [RelayCommand]
    private async Task RefreshAllIconsAsync()
    {
        if (IsRefreshingIcons) return;
        IsRefreshingIcons = true;
        try
        {
            await App.Resolve<MainViewModel>().RefreshAllIconsAsync();
            StatusText = LocalizationService.Instance["IconsRefreshed"];
        }
        catch (Exception)
        {
            StatusText = LocalizationService.Instance["IconsRefreshFailed"];
        }
        finally
        {
            IsRefreshingIcons = false;
        }
    }

    [RelayCommand]
    private void TestNotification()
    {
        var toast = App.Resolve<IToastService>();
        toast.ShowSummary(1);
    }

    partial void OnSoundEffectsEnabledChanged(bool value)
    {
        if (!_initialized) return;
        _config.SoundEffectsEnabled = value;
        _configStore.Save(_config);
        _soundService.SetEnabled(value);
        if (value)
            _soundService.PlaySuccess();
    }

    [RelayCommand]
    private void TestSound()
    {
        _soundService.PlaySuccess();
    }

    [RelayCommand]
    private void Save()
    {
        _config.Language = SelectedLanguageIndex == 0 ? "ru" : "en";
        _config.AutoRefreshIcons = AutoRefreshIconsEnabled;
        _config.AutoCheckEnabled = AutoCheckEnabled;
        _config.ToastNotificationsEnabled = ToastNotificationsEnabled;
        _config.SoundEffectsEnabled = SoundEffectsEnabled;
        _config.CheckIntervalHours = IntervalChoices[Math.Max(0, SelectedIntervalIndex)];
        _config.VersionCacheCount = VersionKeepChoices[Math.Clamp(SelectedVersionKeepIndex, 0, VersionKeepChoices.Count - 1)];
        if (!string.IsNullOrWhiteSpace(PatToken))
            _config.GitHubToken = PatToken.Trim();

        if (!string.IsNullOrWhiteSpace(PortableAppsDirectory))
        {
            var defaultPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AppWatch", "Apps");
            _config.InstallRoot = string.Equals(PortableAppsDirectory.Trim(), defaultPath, StringComparison.OrdinalIgnoreCase)
                ? null
                : PortableAppsDirectory.Trim();
        }

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
            // Installed by Setup.exe → Velopack feed (delta updates, signed nupkg).
            // Only available when VelopackLocator finds a real install; dev/portable
            // builds fall back to the zip flow below.
            _vpManager ??= TryCreateVpManager();
            if (_vpManager is not null)
            {
                var info = await _vpManager.CheckForUpdatesAsync();
                _vpUpdate = info;
                _preparedUpdate = null;
                StatusText = info is null
                    ? LocalizationService.Instance["SettingsNoUpdates"]
                    : string.Format(LocalizationService.Instance["SettingsUpdatesFound"],
                        info.TargetFullRelease.Version);
                OnPropertyChanged(nameof(CanApplySelfUpdate));
                return;
            }

            var version = typeof(SettingsViewModel).Assembly.GetName().Version?.ToString(3) ?? "0.1.0";
            var progress = new Progress<double>(p =>
            {
                StatusText = string.Format(LocalizationService.Instance["SettingsSelfUpdateDownloading"], (int)(p * 100));
            });
            var prepared = await _selfUpdate.PrepareAsync(version, progress, cancellationToken);
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

    // === Velopack self-update (Setup.exe installs) ===

    private UpdateManager? _vpManager;
    private UpdateInfo? _vpUpdate;

    /// <summary>
    /// null when this build isn't Velopack-installed (dotnet run / portable zip),
    /// which is exactly the signal to use the zip flow instead.
    /// </summary>
    private static UpdateManager? TryCreateVpManager()
    {
        try
        {
            if (VelopackLocator.Current is not { } locator || locator.CurrentlyInstalledVersion is null)
                return null;

            // releases.win.json is attached to every GitHub Release by CI (vpk pack)
            return new UpdateManager(new GithubSource("https://github.com/skllfi2/AppWatch", "", false));
        }
        catch
        {
            return null;
        }
    }

    public bool CanApplySelfUpdate =>
        (_preparedUpdate is not null || _vpUpdate is not null) && !SelfUpdateBusy;

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

    public void RefreshStorageSizes()
    {
        var cacheBytes = _versionStore.GetTotalSizeBytes();
        VersionCacheSizeText = FormatBytes(cacheBytes);

        var (tempBytes, _) = GetTempFilesInfo();
        TempFilesSizeText = FormatBytes(tempBytes);
    }

    [RelayCommand]
    private void CleanVersionCache()
    {
        _versionStore.ClearAll();
        RefreshStorageSizes();
        var main = App.Resolve<MainViewModel>();
        foreach (var app in main.Apps)
            app.Refresh();
        StatusText = LocalizationService.Instance["SettingsCacheCleaned"];
    }

    [RelayCommand]
    private void CleanTempFiles()
    {
        var cleanedCount = CleanTempFilesInternal();
        RefreshStorageSizes();
        StatusText = string.Format(LocalizationService.Instance["SettingsTempCleaned"], cleanedCount);
    }

    private static (long TotalBytes, int FileCount) GetTempFilesInfo()
    {
        var tempPath = Path.GetTempPath();
        long bytes = 0;
        int count = 0;
        try
        {
            var di = new DirectoryInfo(tempPath);
            foreach (var file in di.EnumerateFiles("appwatch-*", SearchOption.TopDirectoryOnly))
            {
                try { bytes += file.Length; count++; } catch { }
            }
            foreach (var dir in di.EnumerateDirectories("appwatch-*", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    foreach (var f in dir.EnumerateFiles("*", SearchOption.AllDirectories))
                    {
                        try { bytes += f.Length; count++; } catch { }
                    }
                }
                catch { }
            }
        }
        catch { }
        return (bytes, count);
    }

    private static int CleanTempFilesInternal()
    {
        var tempPath = Path.GetTempPath();
        int deleted = 0;
        try
        {
            var di = new DirectoryInfo(tempPath);
            foreach (var file in di.EnumerateFiles("appwatch-*", SearchOption.TopDirectoryOnly))
            {
                try { file.Delete(); deleted++; } catch { }
            }
            foreach (var dir in di.EnumerateDirectories("appwatch-*", SearchOption.TopDirectoryOnly))
            {
                try { dir.Delete(true); deleted++; } catch { }
            }
        }
        catch { }
        return deleted;
    }

    private static string FormatBytes(long bytes) => bytes switch
    {
        >= 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024 * 1024):F1} ГБ",
        >= 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1} МБ",
        >= 1024 => $"{bytes / 1024.0:F0} КБ",
        _ => $"{bytes} Б"
    };

    [RelayCommand]
    private async Task ExportFullBackupAsync(CancellationToken cancellationToken)
    {
        var picker = new FileSavePicker { SuggestedFileName = $"appwatch-backup-{DateTime.Now:yyyyMMdd-HHmmss}" };
        picker.FileTypeChoices.Add("AppWatch Backup", [".json"]);
        WinRT.Interop.InitializeWithWindow.Initialize(picker,
            WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow));
        var file = await picker.PickSaveFileAsync();
        if (file is null) return;

        var json = JsonSerializer.Serialize(_config, BundleOptions);
        await File.WriteAllTextAsync(file.Path, json, cancellationToken);
        StatusText = LocalizationService.Instance["FullBackupExportDone"];
    }

    [RelayCommand]
    private async Task RestoreFullBackupAsync(CancellationToken cancellationToken)
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

        try
        {
            var text = await File.ReadAllTextAsync(file.Path, cancellationToken);
            var restored = JsonSerializer.Deserialize<AppConfig>(text, BundleOptions);
            if (restored is null || restored.TrackedApps is null)
            {
                StatusText = LocalizationService.Instance["FullBackupRestoreFailed"];
                return;
            }

            _configStore.Save(restored);

            _config.SchemaVersion = restored.SchemaVersion;
            _config.GitHubToken = restored.GitHubToken;
            _config.Language = restored.Language;
            _config.Theme = restored.Theme;
            _config.AutoCheckEnabled = restored.AutoCheckEnabled;
            _config.AutoApplyUpdates = restored.AutoApplyUpdates;
            _config.CheckIntervalHours = restored.CheckIntervalHours;
            _config.DefaultUpdateStrategy = restored.DefaultUpdateStrategy;
            _config.InstallRoot = restored.InstallRoot;
            _config.TrackedApps = restored.TrackedApps;
            _config.DownloadMirrors = restored.DownloadMirrors;
            _config.VersionCacheCount = restored.VersionCacheCount;
            _config.ToastNotificationsEnabled = restored.ToastNotificationsEnabled;
            _config.SoundEffectsEnabled = restored.SoundEffectsEnabled;

            _initialized = false;
            SelectedLanguageIndex = _config.Language == "ru" ? 0 : 1;
            SelectedThemeIndex = (int)_config.Theme;
            AutoCheckEnabled = _config.AutoCheckEnabled;
            ToastNotificationsEnabled = _config.ToastNotificationsEnabled;
            SoundEffectsEnabled = _config.SoundEffectsEnabled;
            SelectedIntervalIndex = IntervalChoices.ToList().IndexOf(_config.CheckIntervalHours);
            if (SelectedIntervalIndex < 0) SelectedIntervalIndex = 2;
            SelectedUpdateStrategyIndex = StrategyChoices.ToList().IndexOf(_config.DefaultUpdateStrategy);
            if (SelectedUpdateStrategyIndex < 0) SelectedUpdateStrategyIndex = 0;
            SelectedVersionKeepIndex = VersionKeepChoices.ToList().IndexOf(_config.VersionCacheCount);
            if (SelectedVersionKeepIndex < 0) SelectedVersionKeepIndex = 1;
            PortableAppsDirectory = _config.InstallRoot
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AppWatch", "Apps");
            PatToken = _config.GitHubToken ?? "";
            _initialized = true;

            LocalizationService.Instance.SetLanguage(_config.Language);
            _theme.SetTheme(_config.Theme);

            var main = App.Resolve<MainViewModel>();
            main.ReloadAppsFromConfig(_config);

            StatusText = string.Format(LocalizationService.Instance["FullBackupRestoreDone"], _config.TrackedApps.Count);
            OnPropertyChanged(nameof(PatStatus));
        }
        catch (Exception ex)
        {
            StatusText = $"{LocalizationService.Instance["FullBackupRestoreFailed"]}: {ex.Message}";
        }
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
        // Velopack path: download the update, then apply + restart (process exits on success).
        if (_vpUpdate is not null && _vpManager is not null)
        {
            var loc = LocalizationService.Instance;
            SelfUpdateBusy = true;
            StatusText = loc["SettingsSelfUpdateApplying"];
            try
            {
                await _vpManager.DownloadUpdatesAsync(_vpUpdate, percent =>
                    StatusText = string.Format(loc["SettingsSelfUpdateDownloading"], percent),
                    cancellationToken);
                _vpManager.ApplyUpdatesAndRestart(_vpUpdate);
            }
            catch (Exception ex)
            {
                StatusText = ex.Message;
            }
            finally
            {
                SelfUpdateBusy = false;
                _vpUpdate = null;
                OnPropertyChanged(nameof(CanApplySelfUpdate));
            }
            return;
        }

        if (_preparedUpdate is null) return;

        // Show self-update dialog with changelog
        var changelog = _preparedUpdate.Version; // TODO: fetch actual changelog from release notes
        var result = await Views.SelfUpdateDialog.ShowAsync(
            _preparedUpdate.Version,
            changelog,
            isZipFlow: true,
            downloadProgress: null); // progress handled internally

        if (result != Views.SelfUpdateResult.UpdateNow)
        {
            if (result == Views.SelfUpdateResult.SkipVersion)
            {
                // TODO: persist skip version
            }
            return;
        }

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
