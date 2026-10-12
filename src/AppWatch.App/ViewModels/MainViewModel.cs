using System.Collections.ObjectModel;
using System.IO;
using AppWatch.Catalog;
using AppWatch.Core.Domain;
using AppWatch.Localization;
using AppWatch.Core.Abstractions;
using AppWatch.Core.Domain.Models;
using AppWatch.Infrastructure;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Media;
using Serilog;

namespace AppWatch.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IConfigStore _configStore;
    private readonly IUpdateEngine _engine;
    private readonly WingetService _winget;
    private readonly CatalogService _catalog;
    private readonly UpdateHistoryStore _history;
    private readonly IAppLauncher _launcher;
    private readonly LocalizationService _loc;
    private readonly AppConfig _config;
    private readonly SystemVersionResolver _systemVersion;

    public MainViewModel(
        IConfigStore configStore, IUpdateEngine engine, WingetService winget,
        CatalogService catalog, UpdateHistoryStore history, IAppLauncher launcher,
        LocalizationService loc, SystemVersionResolver systemVersion)
    {
        _configStore = configStore;
        _engine = engine;
        _winget = winget;
        _catalog = catalog;
        _history = history;
        _launcher = launcher;
        _loc = loc;
        _systemVersion = systemVersion;
        _config = configStore.Load();
        WingetAvailable = winget.IsAvailable;

        foreach (var app in _config.TrackedApps)
            Apps.Add(new TrackedAppItem(app));

        foreach (var entry in _catalog.Entries)
                {
                    var tracked = _config.TrackedApps.Any(
                        a => a.RepoOwner == entry.RepoOwner && a.RepoName == entry.RepoName);
                    CatalogApps.Add(new CatalogItem(entry, tracked));
                }

                // Initialize catalog metrics
                CatalogTotalCount = CatalogApps.Count;
                CatalogTrackedCount = CatalogApps.Count(c => c.IsTracked);
                CatalogUpdatesAvailable = CatalogApps.Count(c => c.UpdateAvailable);

                AppsTrackedCount = Apps.Count;
                                WingetTotalCount = 0;
                                WingetUpdatesAvailable = 0;
                                WingetPendingUpdates = 0;
                        }

                    public ObservableCollection<TrackedAppItem> Apps { get; } = [];

    public ObservableCollection<CatalogItem> CatalogApps { get; } = [];

    public ObservableCollection<WingetItem> WingetUpdates { get; } = [];

    public bool WingetAvailable { get; }

    [ObservableProperty]
    public partial string StatusText { get; set; } = "";

    [ObservableProperty]
        public partial int AppsTrackedCount { get; set; }

        [ObservableProperty]
        public partial int UpdatesAvailableCount { get; set; }

        [ObservableProperty]
        public partial int UpdatedTodayCount { get; set; }

        [ObservableProperty]
        public partial int CatalogTotalCount { get; set; }

        [ObservableProperty]
        public partial int CatalogTrackedCount { get; set; }

        [ObservableProperty]
        public partial int CatalogUpdatesAvailable { get; set; }

        [ObservableProperty]
        public partial int WingetTotalCount { get; set; }

        [ObservableProperty]
        public partial int WingetUpdatesAvailable { get; set; }

        [ObservableProperty]
        public partial int WingetPendingUpdates { get; set; }

        public string HeroTitle => LocalizationService.Instance["HeroTitle"];
        public string HeroMetricTracked => string.Format(LocalizationService.Instance["HeroMetricTracked"], AppsTrackedCount);
        public string HeroMetricUpdates => string.Format(LocalizationService.Instance["HeroMetricUpdates"], UpdatesAvailableCount);
        public string HeroMetricUpdatedToday => string.Format(LocalizationService.Instance["HeroMetricUpdatedToday"], UpdatedTodayCount);

        public string CatalogHeroTitle => LocalizationService.Instance["CatalogHeroTitle"];
        public string CatalogMetricTotal => string.Format(LocalizationService.Instance["CatalogMetricTotal"], CatalogTotalCount);
        public string CatalogMetricTracked => string.Format(LocalizationService.Instance["CatalogMetricTracked"], CatalogTrackedCount);
        public string CatalogMetricUpdates => string.Format(LocalizationService.Instance["CatalogMetricUpdates"], CatalogUpdatesAvailable);

        public string WingetHeroTitle => LocalizationService.Instance["WingetHeroTitle"];
        public string WingetMetricTotal => string.Format(LocalizationService.Instance["WingetMetricTotal"], WingetTotalCount);
        public string WingetMetricUpdates => string.Format(LocalizationService.Instance["WingetMetricUpdates"], WingetUpdatesAvailable);
        public string WingetMetricPending => string.Format(LocalizationService.Instance["WingetMetricPending"], WingetPendingUpdates);

            partial void OnAppsTrackedCountChanged(int value) => OnPropertyChanged(nameof(HeroMetricTracked));
            partial void OnUpdatesAvailableCountChanged(int value) => OnPropertyChanged(nameof(HeroMetricUpdates));
            partial void OnUpdatedTodayCountChanged(int value) => OnPropertyChanged(nameof(HeroMetricUpdatedToday));
            partial void OnCatalogTotalCountChanged(int value) => OnPropertyChanged(nameof(CatalogMetricTotal));
            partial void OnCatalogTrackedCountChanged(int value) => OnPropertyChanged(nameof(CatalogMetricTracked));
            partial void OnCatalogUpdatesAvailableChanged(int value) => OnPropertyChanged(nameof(CatalogMetricUpdates));
            partial void OnWingetTotalCountChanged(int value) => OnPropertyChanged(nameof(WingetMetricTotal));
            partial void OnWingetUpdatesAvailableChanged(int value) => OnPropertyChanged(nameof(WingetMetricUpdates));
            partial void OnWingetPendingUpdatesChanged(int value) => OnPropertyChanged(nameof(WingetMetricPending));

            public Brush StatusDotBrush => UpdatesAvailableCount > 0
                ? new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 0x2E, 0xA0, 0x43))
                : new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 0x61, 0x61, 0x61));

            public string InstallButtonText => _loc["install"];

        [RelayCommand]
        private void AddCustomApp()
        {
            // TODO: Show dialog to add custom app to catalog
            StatusText = _loc["notImplemented"];
        }

    private string InstallRoot => _config.InstallRoot
        ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AppWatch", "Apps");

    [RelayCommand]
    private async Task InstallFromCatalogAsync(CatalogItem? item, CancellationToken cancellationToken)
    {
        if (item is null) return;
        if (item.IsTracked)
        {
            item.InstallStatus = _loc["alreadyTracked"];
            return;
        }

        StatusText = _loc.Format("installing", item.Name);
        var app = item.Entry.ToTrackedApp(Path.Combine(InstallRoot, item.Entry.RepoName));
        var history = await _engine.InstallAsync(app, cancellationToken);
        item.InstallStatus = history.Success ? _loc["installed"] : _loc["installFailed"];

        if (history.Success)
        {
            item.IsTracked = true;
            _config.TrackedApps.Add(app);
            _configStore.Save(_config);
            Apps.Add(new TrackedAppItem(app));
            AppsTrackedCount = Apps.Count;
        }
        StatusText = _loc["checkDone"];
    }

    [RelayCommand]
    private async Task CheckUpdatesAsync(CancellationToken cancellationToken)
    {
        StatusText = _loc["Checking"];
        foreach (var item in Apps)
        {
            item.Stage = UpdateStage.Checking;
            var result = await _engine.CheckAsync(item.App, cancellationToken);
            item.StatusText = Describe(result.Status);
            item.UpdateAvailable = result.Status == UpdateStatus.UpdateAvailable;
            item.Stage = result.Status switch
            {
                UpdateStatus.Error => UpdateStage.Error,
                UpdateStatus.UpdateAvailable => UpdateStage.Idle,
                _ => UpdateStage.Done,
            };
        }
        UpdatesAvailableCount = Apps.Count(a => a.UpdateAvailable);
        StatusText = _loc["CheckDone"];
    }

    [RelayCommand]
    private async Task UpdateAllAsync(CancellationToken cancellationToken)
    {
        foreach (var item in Apps.Where(a => a.App.UpdateKind != UpdateKind.WingetPackage))
        {
            item.Stage = UpdateStage.Checking;
            var check = await _engine.CheckAsync(item.App, cancellationToken);
            item.StatusText = Describe(check.Status);
            item.UpdateAvailable = check.Status == UpdateStatus.UpdateAvailable;
            if (check.Status != UpdateStatus.UpdateAvailable)
            {
                item.Stage = check.Status == UpdateStatus.Error ? UpdateStage.Error : UpdateStage.Done;
                continue;
            }

            StatusText = _loc.Format("Updating", item.Name);
            item.Stage = UpdateStage.Downloading;
            item.ProgressValue = 0;
            var download = new Progress<double>(p => item.ProgressValue = p);
            var history = await _engine.ApplyAsync(item.App, check, download, cancellationToken);
            if (history.Success)
            {
                item.Stage = UpdateStage.Done;
                item.UpdateAvailable = false;
                item.ReportVersionChanged();
                UpdatedTodayCount++;
            }
            else
            {
                item.Stage = UpdateStage.Error;
            }
            item.StatusText = history.Success ? _loc["Updated"] : _loc["UpdateFailed"];
        }

        UpdatesAvailableCount = Apps.Count(a => a.UpdateAvailable);
        _configStore.Save(_config);
        StatusText = _loc["CheckDone"];
    }

    [RelayCommand]
    private async Task UpdateOneAsync(TrackedAppItem? item, CancellationToken cancellationToken)
    {
        if (item is null) return;

        StatusText = _loc.Format("updating", item.Name);
        item.Stage = UpdateStage.Checking;
        var check = await _engine.CheckAsync(item.App, cancellationToken);
        item.StatusText = Describe(check.Status);
        if (check.Status != UpdateStatus.UpdateAvailable)
        {
            item.Stage = check.Status == UpdateStatus.Error ? UpdateStage.Error : UpdateStage.Done;
            return;
        }

        item.Stage = UpdateStage.Downloading;
        item.ProgressValue = 0;
        var download = new Progress<double>(p => item.ProgressValue = p);
        var history = await _engine.ApplyAsync(item.App, check, download, cancellationToken);
        if (history.Success)
        {
            item.Stage = UpdateStage.Done;
            item.UpdateAvailable = false;
            item.ReportVersionChanged();
            UpdatedTodayCount++;
        }
        else
        {
            item.Stage = UpdateStage.Error;
        }
        item.StatusText = history.Success ? _loc["updated"] : _loc["updateFailed"];
        UpdatesAvailableCount = Apps.Count(a => a.UpdateAvailable);
        _configStore.Save(_config);
        StatusText = _loc["checkDone"];
    }

    [RelayCommand]
    private void LaunchApp(TrackedAppItem? item)
    {
        if (item?.App.InstallDirectory is null || item.App.ExecutableName is null) return;
        _launcher.Launch(Path.Combine(item.App.InstallDirectory, item.App.ExecutableName));
    }

    [RelayCommand]
        private async Task ScanWingetAsync(CancellationToken cancellationToken)
        {
            StatusText = _loc["WingetScanning"];
            try
            {
                var updates = await _winget.GetUpgradesAsync(cancellationToken);
                var filtered = WingetUpgradeFilter.Filter(
                    updates, name => _systemVersion.ResolveInstalledVersion(name));
                WingetUpdates.Clear();
                foreach (var row in filtered.Visible)
                    WingetUpdates.Add(new WingetItem(row));

                // Update winget metrics
                WingetTotalCount = updates.Count;
                WingetUpdatesAvailable = filtered.Visible.Count;
                WingetPendingUpdates = filtered.Hidden.Count;

                StatusText = filtered.Hidden.Count > 0
                    ? _loc.Format("WingetFoundHidden", filtered.Visible.Count, filtered.Hidden.Count)
                    : _loc.Format("WingetFound", filtered.Visible.Count);
            }
            catch (InvalidOperationException)
            {
                StatusText = _loc["WingetUnavailable"];
                WingetTotalCount = 0;
                WingetUpdatesAvailable = 0;
                WingetPendingUpdates = 0;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "winget scan failed");
                StatusText = _loc["error"];
                WingetTotalCount = 0;
                WingetUpdatesAvailable = 0;
                WingetPendingUpdates = 0;
            }
        }

    [RelayCommand]
    private async Task UpdateSelectedWingetAsync(CancellationToken cancellationToken)
    {
        var selected = WingetUpdates.Where(w => w.IsSelected).ToList();
        if (selected.Count == 0) return;

        foreach (var item in selected)
        {
            item.Status = _loc["WingetWaiting"];
            item.ProgressValue = 0;
            item.IsUpdating = true;
            var progress = new Progress<double>(p =>
            {
                item.ProgressValue = p < 100 ? p : Math.Min(100, p - 100);
                item.Status = p < 100
                    ? _loc.Format("WingetDownloading", (int)p)
                    : _loc.Format("WingetInstalling", (int)Math.Min(100, p - 100));
            });
            var (ok, message) = await _winget.UpgradePackageAsync(item.Id, item.Name, progress, cancellationToken);
            _history.Append(new UpdateHistoryEntry(
                item.Name, item.Row.Update.CurrentVersion, item.Row.Update.AvailableVersion,
                DateTimeOffset.UtcNow, ok, ok ? null : message));
            item.IsUpdating = false;
            item.Status = ok ? _loc["WingetDone"] : $"{_loc["WingetFailed"]}: {message}";
        }
        StatusText = _loc["checkDone"];
    }

    /// <summary>
    /// Silent check + auto-apply for scheduled background runs (called by the App timer).
    /// </summary>
    public async Task RunBackgroundCheckAsync()
    {
        foreach (var item in Apps.Where(a => a.App.UpdateKind != UpdateKind.WingetPackage && a.App.Enabled))
        {
            item.Stage = UpdateStage.Checking;
            var check = await _engine.CheckAsync(item.App);
            item.StatusText = Describe(check.Status);
            item.UpdateAvailable = check.Status == UpdateStatus.UpdateAvailable;
            if (check.Status != UpdateStatus.UpdateAvailable)
            {
                item.Stage = check.Status == UpdateStatus.Error ? UpdateStage.Error : UpdateStage.Done;
                continue;
            }

            item.StatusText = _loc.Format("updating", item.Name);
            item.Stage = UpdateStage.Downloading;
            item.ProgressValue = 0;
            var download = new Progress<double>(p => item.ProgressValue = p);
            var history = await _engine.ApplyAsync(item.App, check, download);
            if (history.Success)
            {
                item.Stage = UpdateStage.Done;
                item.UpdateAvailable = false;
                item.ReportVersionChanged();
                UpdatedTodayCount++;
            }
            else
            {
                item.Stage = UpdateStage.Error;
            }
            item.StatusText = history.Success ? _loc["Updated"] : _loc["UpdateFailed"];
        }
        UpdatesAvailableCount = Apps.Count(a => a.UpdateAvailable);
        _configStore.Save(_config);
    }

    private string Describe(UpdateStatus status) => status switch
    {
        UpdateStatus.UpToDate => _loc["UpToDate"],
        UpdateStatus.UpdateAvailable => _loc["UpdateAvailable"],
        UpdateStatus.Pinned => _loc["Pinned"],
        UpdateStatus.NoMatchingAsset => _loc["NoMatchingAsset"],
        UpdateStatus.Disabled => _loc["Disabled"],
        UpdateStatus.Error => _loc["Error"],
        _ => status.ToString(),
    };
}
