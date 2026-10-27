using System.Collections.ObjectModel;
using System.IO;
using AppWatch.Catalog;
using AppWatch.Core.Domain;
using AppWatch.Localization;
using AppWatch.Core.Abstractions;
using AppWatch.Core.Domain.Models;
using AppWatch.Infrastructure;
using AppWatch.Platform;
using AppWatch.Views;  // for AddEditAppDialog
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Dispatching;
using Windows.Storage.Pickers;
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
    private readonly IProcessService _process;
    private readonly LocalizationService _loc;
    private readonly AppConfig _config;
    private readonly SystemVersionResolver _systemVersion;
    private readonly UpdateJournalStore _journal;
    private readonly Infrastructure.System.FileStore _files;

    public MainViewModel(
        IConfigStore configStore, IUpdateEngine engine, WingetService winget,
        CatalogService catalog, UpdateHistoryStore history, IAppLauncher launcher,
        LocalizationService loc, SystemVersionResolver systemVersion, IProcessService process,
        UpdateJournalStore journal, Infrastructure.System.FileStore files)
    {
        _configStore = configStore;
        _engine = engine;
        _winget = winget;
        _catalog = catalog;
        _history = history;
        _launcher = launcher;
        _process = process;
        _loc = loc;
        _systemVersion = systemVersion;
        _journal = journal;
        _files = files;
        _config = configStore.Load();
        WingetAvailable = winget.IsAvailable;

        foreach (var app in _config.TrackedApps)
        {
            var item = new TrackedAppItem(app);
            item.PropertyChanged += OnHomeItemChanged;
            Apps.Add(item);
        }

        foreach (var entry in _catalog.Entries)
        {
            var tracked = _config.TrackedApps.Any(
                a => a.RepoOwner == entry.RepoOwner && a.RepoName == entry.RepoName);
            var item = new CatalogItem(entry, tracked)
            {
                IsUserEntry = _catalog.IsUserEntry(entry.RepoOwner, entry.RepoName),
            };
            CatalogApps.Add(item);
        }

        // Initialize catalog metrics + sources + search filter
        CatalogTotalCount = CatalogApps.Count;
        CatalogTrackedCount = CatalogApps.Count(c => c.IsTracked);
        CatalogUpdatesAvailable = CatalogApps.Count(c => c.UpdateAvailable);
        RefreshSources();
        ApplyCatalogFilter();
        RecomputeHomeGroups(); // initial population of the four home sections
        RefreshNextCheckText();
        RefreshLastCheckText();

        _footerTimer.Tick += (_, _) => RefreshFooter();
        _statusResetTimer.Tick += (_, _) => StatusText = "";
        _footerTimer.Start();

                AppsTrackedCount = Apps.Count;
                                WingetTotalCount = 0;
                                WingetUpdatesAvailable = 0;
                                WingetPendingUpdates = 0;

        // Keep the home groups in sync whenever apps are added/removed from any path
        Apps.CollectionChanged += (_, args) =>
        {
            if (args.NewItems is not null)
                foreach (TrackedAppItem added in args.NewItems)
                    added.PropertyChanged += OnHomeItemChanged;
            RecomputeHomeGroups();
                    };
                }

                public ObservableCollection<TrackedAppItem> Apps { get; } = [];

    // === Home page: grouped sections (Layer-1 operation-centric rework) ===
    // Filters/sort combos are gone — apps are always visible, split by what
    // they need from the user: update / attention / fine / off.

    public ObservableCollection<TrackedAppItem> UpdatesGroup { get; } = [];
    public ObservableCollection<TrackedAppItem> ErrorsGroup { get; } = [];
    public ObservableCollection<TrackedAppItem> UpToDateGroup { get; } = [];
    public ObservableCollection<TrackedAppItem> DisabledGroup { get; } = [];

    public bool HasUpdates => UpdatesGroup.Count > 0;
    public bool HasErrors => ErrorsGroup.Count > 0;
    public bool HasUpToDate => UpToDateGroup.Count > 0;
    public bool HasDisabled => DisabledGroup.Count > 0;

    public string UpdatesHeaderText => _loc.Format("GroupUpdates", UpdatesGroup.Count);
    public string ErrorsHeaderText => _loc.Format("GroupErrors", ErrorsGroup.Count);
    public string UpToDateHeaderText => _loc.Format("GroupUpToDate", UpToDateGroup.Count);
    public string DisabledHeaderText => _loc.Format("GroupDisabled", DisabledGroup.Count);

    /// <summary>Recomputes the four home groups from the live item state.</summary>
    private void RecomputeHomeGroups()
    {
        FillGroup(UpdatesGroup, Apps
            .Where(a => a.App.Enabled && a.UpdateAvailable)
            .OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase));
        FillGroup(ErrorsGroup, Apps
            .Where(a => a.App.Enabled && !a.UpdateAvailable && a.Stage == UpdateStage.Error)
            .OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase));
        FillGroup(UpToDateGroup, Apps
            .Where(a => a.App.Enabled && !a.UpdateAvailable && a.Stage != UpdateStage.Error)
            .OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase));
        FillGroup(DisabledGroup, Apps
            .Where(a => !a.App.Enabled)
            .OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase));

        foreach (var p in new[]
                 {
                     nameof(HasUpdates), nameof(HasErrors), nameof(HasUpToDate), nameof(HasDisabled),
                     nameof(UpdatesHeaderText), nameof(ErrorsHeaderText),
                     nameof(UpToDateHeaderText), nameof(DisabledHeaderText),
                 })
            OnPropertyChanged(p);
        RefreshFooter();
    }

    private static void FillGroup(ObservableCollection<TrackedAppItem> group, IEnumerable<TrackedAppItem> items)
    {
        group.Clear();
        foreach (var i in items)
            group.Add(i);
    }

    /// <summary>Item state changed (stage/update flag) — keep the groups live.</summary>
    private void OnHomeItemChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(TrackedAppItem.UpdateAvailable) or nameof(TrackedAppItem.Stage))
            RecomputeHomeGroups();
    }

    [ObservableProperty]
    public partial string NextCheckText { get; set; } = "";

    /// <summary>
    /// Bottom bar: operation status while something runs, otherwise a live summary
    /// ("7 приложений · 1 обновление · след. проверка 02:37") refreshed every 30 s.
    /// </summary>
    public string FooterText => string.IsNullOrWhiteSpace(StatusText) ? BuildFooterSummary() : StatusText;

    partial void OnStatusTextChanged(string value)
    {
        OnPropertyChanged(nameof(FooterText));
        // transient op messages: let the live summary take the footer back ~20 s
        // after the last status change (op completion texts, not busy indicators).
        if (string.IsNullOrEmpty(value)) return;
        _statusResetTimer.Stop();
        _statusResetTimer.Start();
    }

    private readonly Microsoft.UI.Xaml.DispatcherTimer _statusResetTimer = new() { Interval = TimeSpan.FromSeconds(20) };

    private readonly Microsoft.UI.Xaml.DispatcherTimer _footerTimer =
        new() { Interval = TimeSpan.FromSeconds(30) };

    private string BuildFooterSummary()
    {
        // Quiet state = schedule info only (user-facing decision 2026-10-24):
        // «Следующая проверка» + «Последняя проверка: дата и время».
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(NextCheckText))
            parts.Add(NextCheckText);
        if (!string.IsNullOrEmpty(LastCheckText))
            parts.Add(LastCheckText);
        return string.Join("  ·  ", parts);
    }

    private void RefreshFooter()
    {
        // keep the schedule texts fresh alongside the footer itself (30 s tick)
        RefreshNextCheckText();
        RefreshLastCheckText();
        OnPropertyChanged(nameof(FooterText));
    }

    public ObservableCollection<CatalogItem> CatalogApps { get; } = [];

    public ObservableCollection<WingetItem> WingetUpdates { get; } = [];

        public bool WingetAvailable { get; }

        [ObservableProperty]
        public partial int SelectedTabIndex { get; set; } = 0;

    // === Catalog category filter (P-UI) ===

    /// <summary>0 = all, 1 = utility, 2 = media, 3 = network, 4 = security, 5 = productivity, 6 = development.</summary>
    [ObservableProperty]
    public partial int CatalogCategoryIndex { get; set; }

    public IReadOnlyList<string> CatalogCategories { get; } =
        ["utility", "media", "network", "security", "productivity", "development"];

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

                [ObservableProperty]
                public partial bool IsUpdateAllRunning { get; set; }

                private CancellationTokenSource? _updateAllCts;

                    [ObservableProperty]
                    public partial string NewSourceName { get; set; } = "";

                    [ObservableProperty]
                    public partial string NewSourceUrl { get; set; } = "";

                    [ObservableProperty]
                    public partial bool IsAddingSource { get; set; }

                    /// <summary>All catalog sources for UI display.</summary>
                    public ObservableCollection<CatalogSourceItem> CatalogSources { get; } = [];

                    private void RefreshSources()
                    {
                        CatalogSources.Clear();
                        foreach (var s in _catalog.Sources)
                            CatalogSources.Add(new CatalogSourceItem(s, _catalog));
                    }

                    public string HeroTitle => LocalizationService.Instance["HeroTitle"];
        public string AppsTrackedDisplay => string.Format(Loc.Instance["HeroMetricTracked"], AppsTrackedCount);
                public string UpdatesAvailableDisplay => string.Format(Loc.Instance["HeroMetricUpdates"], UpdatesAvailableCount);
                public string UpdatedTodayDisplay => string.Format(Loc.Instance["HeroMetricUpdatedToday"], UpdatedTodayCount);
                public string NextCheckDisplay => string.Format(Loc.Instance["HeroMetricNextCheck"], NextCheckText);

        public string CatalogHeroTitle => LocalizationService.Instance["CatalogHeroTitle"];
        public string CatalogMetricTotal => string.Format(LocalizationService.Instance["CatalogMetricTotal"], CatalogTotalCount);
        public string CatalogMetricTracked => string.Format(LocalizationService.Instance["CatalogMetricTracked"], CatalogTrackedCount);
        public string CatalogMetricUpdates => string.Format(LocalizationService.Instance["CatalogMetricUpdates"], CatalogUpdatesAvailable);

        public string WingetHeroTitle => LocalizationService.Instance["WingetHeroTitle"];
        public string WingetMetricTotal => string.Format(LocalizationService.Instance["WingetMetricTotal"], WingetTotalCount);
        public string WingetMetricUpdates => string.Format(LocalizationService.Instance["WingetMetricUpdates"], WingetUpdatesAvailable);
        public string WingetMetricPending => string.Format(LocalizationService.Instance["WingetMetricPending"], WingetPendingUpdates);

            partial void OnAppsTrackedCountChanged(int value) => OnPropertyChanged(nameof(AppsTrackedDisplay));
                    partial void OnUpdatesAvailableCountChanged(int value) => OnPropertyChanged(nameof(UpdatesAvailableDisplay));
                    partial void OnUpdatedTodayCountChanged(int value) => OnPropertyChanged(nameof(UpdatedTodayDisplay));
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
                private async Task AddCustomAppAsync()
                {
                    var dialog = new AddEditAppDialog();
                    dialog.XamlRoot = App.MainWindow!.Content.XamlRoot;
                    var result = await dialog.ShowAsync();
                    if (result == ContentDialogResult.Primary)
                    {
                        var app = dialog.ViewModel.ToTrackedApp();
                        var installRoot = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                            "AppWatch", "Apps", app.RepoName);
                        // Default only when the user didn't pick/enter a directory in the dialog
                        if (string.IsNullOrWhiteSpace(app.InstallDirectory))
                            app.InstallDirectory = installRoot;
                        _config.TrackedApps.Add(app);
                        _configStore.Save(_config);
                        Apps.Add(new TrackedAppItem(app));
                        AppsTrackedCount = Apps.Count;
                        StatusText = _loc["installed"];
                    }
                }

                [RelayCommand]
                private async Task EditAppAsync(TrackedAppItem? item)
                {
                    if (item is null) return;
                    var dialog = new AddEditAppDialog(item.App);
                    dialog.XamlRoot = App.MainWindow!.Content.XamlRoot;
                    var result = await dialog.ShowAsync();
                    if (result == ContentDialogResult.Primary)
                    {
                        dialog.ViewModel.ToTrackedApp();
                        _configStore.Save(_config);
                        item.Refresh();
                        StatusText = _loc["SettingsSaved"];
                    }
                }

                // === B10: version cache rollback ===

                [RelayCommand]
                private async Task RollbackAppAsync(TrackedAppItem? item)
                {
                    if (item is null || item.App.InstallDirectory is null) return;

                    var dialog = new RollbackVersionDialog(item.App);
                    dialog.XamlRoot = App.MainWindow!.Content.XamlRoot;
                    if (await dialog.ShowAsync() != ContentDialogResult.Primary || dialog.SelectedEntry is null)
                        return;

                    var target = dialog.SelectedEntry;
                    var app = item.App;
                    var processName = app.ProcessName
                        ?? (app.ExecutableName is null ? null : Path.GetFileNameWithoutExtension(app.ExecutableName));
                    if (processName is not null && _process.IsRunning(processName))
                        _process.Kill(processName);

                    var versions = App.Resolve<Infrastructure.System.VersionStore>();
                    versions.RollbackTo(app.InstallDirectory!, app.Name, target);

                    app.CurrentVersion = target.DisplayVersion ?? target.Label; // D5: human-readable
                    _configStore.Save(_config);
                    item.Refresh();
                    item.Stage = UpdateStage.Done;
                    item.StatusText = _loc["RollbackDone"];
                    StatusText = _loc["RollbackDone"];
                }

                // === A1: pin / skip / reset per-app update rules ===

                [RelayCommand]
                private void SkipVersion(TrackedAppItem? item)
                {
                    if (item is null) return;
                    var app = item.App;
                    var newest = item.LatestTag;
                    if (newest is null) return;
                    app.SkippedVersion = newest;
                    _configStore.Save(_config);
                    item.Refresh();
                    StatusText = _loc.Format("SkipVersionDone", newest);
                }

                [RelayCommand]
                private void PinCurrent(TrackedAppItem? item)
                {
                    if (item is null || item.App.CurrentVersion is null) return;
                    var app = item.App;
                    if (app.PinnedVersion is null)
                    {
                        app.PinnedVersion = app.CurrentVersion;
                        StatusText = _loc.Format("PinCurrentDone", app.CurrentVersion);
                    }
                    else
                    {
                        app.PinnedVersion = null;
                        StatusText = _loc["PinCleared"];
                    }
                    _configStore.Save(_config);
                    item.Refresh();
                }

                [RelayCommand]
                private void ResetRules(TrackedAppItem? item)
                {
                    if (item is null) return;
                    item.App.SkippedVersion = null;
                    item.App.PinnedVersion = null;
                    _configStore.Save(_config);
                    item.Refresh();
                    StatusText = _loc["RulesReset"];
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

    // === User catalog: add/remove/search (closes "решаем за пользователя") ===

    [RelayCommand]
    private async Task AddToCatalogAsync()
    {
        var dialog = new Views.AddEditAppDialog();
        dialog.XamlRoot = App.MainWindow!.Content.XamlRoot;
        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
            return;

        var vm = dialog.ViewModel;
        var entry = new CatalogEntry
        {
            Name = vm.Name.Trim(),
            RepoOwner = vm.RepoOwner.Trim(),
            RepoName = vm.RepoName.Trim(),
            UpdateKind = (UpdateKind)vm.SelectedUpdateKindIndex,
            ExecutableName = string.IsNullOrWhiteSpace(vm.ExecutableName) ? null : vm.ExecutableName.Trim(),
            ProcessName = string.IsNullOrWhiteSpace(vm.ProcessName) ? null : vm.ProcessName.Trim(),
            Description = null,
        };

        if (_catalog.AddUserEntry(entry))
        {
            RefreshCatalogItems();
            StatusText = _loc.Format("AddedToCatalog", entry.Name);
        }
        else
        {
            StatusText = _loc["CatalogSaveFailed"];
        }
    }

    [RelayCommand]
    private void RemoveFromCatalog(CatalogItem? item)
    {
        if (item is null || !item.IsUserEntry) return;
        if (_catalog.RemoveUserEntry(item.Entry.RepoOwner, item.Entry.RepoName))
        {
            RefreshCatalogItems();
            StatusText = _loc.Format("RemovedFromCatalog", item.Name);
        }
    }

    // === Catalog sources: subscribe to external catalogs ===
    [RelayCommand]
    private async Task AddCatalogSourceAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(NewSourceName) || string.IsNullOrWhiteSpace(NewSourceUrl))
        {
            StatusText = _loc["SourceNameUrlRequired"];
            return;
        }
        IsAddingSource = true;
        StatusText = _loc["SourceDownloading"];
        try
        {
            var error = await _catalog.AddRemoteSourceAsync(NewSourceName.Trim(), NewSourceUrl.Trim());
            if (error is not null)
            {
                StatusText = _loc.Format("SourceAddFailed", error);
            }
            else
            {
                NewSourceName = "";
                NewSourceUrl = "";
                StatusText = _loc.Format("SourceAdded", NewSourceName);
                RefreshSources();
                RefreshCatalogItems();
            }
        }
        finally
        {
            IsAddingSource = false;
        }
    }

    [RelayCommand]
    private void RemoveCatalogSource(CatalogSource? source)
    {
        if (source is null || source.Type == CatalogSourceType.Seed) return;
        if (_catalog.RemoveSource(source.Id))
        {
            RefreshSources();
            RefreshCatalogItems();
            StatusText = _loc.Format("SourceRemoved", source.Name);
        }
    }

    [RelayCommand]
    private void ToggleCatalogSource(CatalogSource? source)
    {
        if (source is null) return;
        _catalog.ToggleSource(source.Id);
        RefreshSources();
        RefreshCatalogItems();
    }

    [RelayCommand]
    private async Task RefreshCatalogSourceAsync(CatalogSource? source)
    {
        if (source is null || source.Type != CatalogSourceType.RemoteUrl) return;
        StatusText = _loc["SourceDownloading"];
        var error = await _catalog.RefreshSourceAsync(source.Id);
        StatusText = error is null ? _loc["SourceRefreshed"] : _loc.Format("SourceAddFailed", error);
        RefreshSources();
        RefreshCatalogItems();
    }

    // === Import from GitHub Stars ===

    [RelayCommand]
    private async Task ImportFromStarsAsync()
    {
        try
        {
            var dialog = new Views.ImportFromStarsDialog(_catalog);
            dialog.XamlRoot = App.MainWindow!.Content.XamlRoot;
            var result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary)
                return;

            var entries = dialog.GetSelectedEntries();
            if (entries.Count == 0)
            {
                StatusText = _loc["ImportStarsNothingSelected"];
                return;
            }

            var added = 0;
            foreach (var entry in entries)
            {
                if (_catalog.AddUserEntry(entry))
                    added++;
            }

            RefreshCatalogItems();
            StatusText = added > 0
                ? _loc.Format("ImportStarsDone", added)
                : _loc["ImportStarsNoneAdded"];
        }
        catch (System.Exception ex)
        {
            // Diagnostics: surface the failure in the status bar instead of crashing
            StatusText = "ImportFromStars: " + ex.GetType().Name + ": " + ex.Message;
        }
    }

    // === Clear user catalog ("мои приложения") ===

    [RelayCommand]
    private async Task ClearMyCatalogAsync()
    {
        if (_catalog.UserEntries.Count == 0)
        {
            StatusText = _loc["ImportStarsNoneAdded"];
            return;
        }

        var confirm = new ContentDialog
        {
            Title = _loc["ClearCatalogConfirmTitle"],
            Content = _loc.Format("ClearCatalogConfirmText", _catalog.UserEntries.Count),
            PrimaryButtonText = _loc["BtnConfirmClear"],
            CloseButtonText = _loc["Cancel"],
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = App.MainWindow!.Content.XamlRoot,
        };
        if (await confirm.ShowAsync() != ContentDialogResult.Primary)
            return;

        var removed = _catalog.ClearUserEntries();
        RefreshCatalogItems();
        StatusText = removed > 0
            ? _loc.Format("CatalogCleared", removed)
            : _loc["ImportStarsNoneAdded"];
    }

        private void RefreshCatalogItems()
    {
        CatalogApps.Clear();
        foreach (var entry in _catalog.Entries)
        {
            var tracked = _config.TrackedApps.Any(
                a => a.RepoOwner == entry.RepoOwner && a.RepoName == entry.RepoName);
            var repoKey = $"{entry.RepoOwner}/{entry.RepoName}";
            CatalogApps.Add(new CatalogItem(entry, tracked)
            {
                IsUserEntry = _catalog.IsUserEntry(entry.RepoOwner, entry.RepoName),
                SourceName = _catalog.SourceByRepo.TryGetValue(repoKey, out var src) ? src : null,
            });
        }
        CatalogTotalCount = CatalogApps.Count;
        CatalogTrackedCount = CatalogApps.Count(c => c.IsTracked);
        ApplyCatalogFilter();
    }

    [ObservableProperty]
    public partial string CatalogSearchQuery { get; set; } = "";

    partial void OnCatalogSearchQueryChanged(string value)
    {
        ApplyCatalogFilter();
    }

    /// <summary>Filtered catalog: lexical fuzzy tiers (CatalogSearch).</summary>
    public ObservableCollection<CatalogItem> FilteredCatalogApps { get; } = [];

    partial void OnCatalogCategoryIndexChanged(int value) => ApplyCatalogFilter();

    private void ApplyHomeFilter()
    {
        // Legacy hook kept for callers; grouping replaced the flat filter.
        RecomputeHomeGroups();
    }

    /// <summary>Localized "next check at HH:mm" from the monitor's persisted schedule.</summary>
    public void RefreshNextCheckText()
    {
        var next = _configStore.Load().BackgroundCheckNextRunUtc;
        NextCheckText = next is { } n
            ? _loc.Format("NextCheckAt", n.ToLocalTime().ToString("HH:mm"))
            : "";
    }

    [ObservableProperty]
    public partial string LastCheckText { get; set; } = "";

    public void RefreshLastCheckText()
    {
        var last = _configStore.Load().LastCheckUtc;
        LastCheckText = last is { } l
            ? _loc.Format("LastCheckAt", l.ToLocalTime().ToString("dd.MM.yyyy HH:mm"))
            : "";
    }

    [RelayCommand]
        private void GoToCatalog() => App.MainWindow!.NavigateTo(typeof(Views.CatalogPage));

        [RelayCommand]
        private void GoToWinget() => App.MainWindow!.NavigateTo(typeof(Views.WingetPage));

    [RelayCommand]
    private void GoToHistory() => App.MainWindow!.NavigateTo(typeof(Views.HistoryPage));

    /// <summary>
    /// Uninstalls an app installed/tracked by AppWatch, with an explicit confirm.
    /// Managed portables are deleted from %LOCALAPPDATA%\AppWatch\Apps; winget
    /// packages go through winget uninstall; anything else (user's own dirs,
    /// installer-kind) is untracked without touching files. The confirm dialog
    /// says which of the three will happen.
    /// </summary>
    [RelayCommand]
    private async Task UninstallAppAsync(TrackedAppItem? item, CancellationToken cancellationToken)
    {
        if (item is null) return;

        var isWinget = item.App.UpdateKind == UpdateKind.WingetPackage;
        var managedRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AppWatch", "Apps");
        var isManagedPortable = item.App.UpdateKind is UpdateKind.PortableZip or UpdateKind.PortableExe
            && item.App.InstallDirectory is not null
            && Path.GetFullPath(item.App.InstallDirectory)
                .StartsWith(Path.GetFullPath(managedRoot), StringComparison.OrdinalIgnoreCase);

        var whatHappens = isWinget
            ? _loc["UninstallConfirmWinget"]
            : isManagedPortable
                ? _loc["UninstallConfirmDelete"]
                : _loc["UninstallConfirmUntrack"];

        var confirm = new ContentDialog
        {
            Title = _loc.Format("UninstallConfirmTitle", item.Name),
            Content = whatHappens,
            PrimaryButtonText = _loc["MenuUninstall"],
            CloseButtonText = _loc["Cancel"],
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = App.MainWindow!.Content.XamlRoot,
        };
        if (await confirm.ShowAsync() != ContentDialogResult.Primary)
            return;

        var history = await _engine.UninstallAsync(item.App, cancellationToken);
        _history.Append(history);

        if (history.Success)
        {
            _config.TrackedApps.Remove(item.App);
            Apps.Remove(item);
            _configStore.Save(_config);
            AppsTrackedCount = Apps.Count;
            UpdatesAvailableCount = Apps.Count(a => a.UpdateAvailable);
            StatusText = _loc.Format("UninstallDone", item.Name);
        }
        else
                    {
                        StatusText = _loc["UninstallFailed"] + ": " + UpdateErrorHints.WithHint(history.Detail, k => _loc[k]);
                    }
                }

            /// <summary>
                        /// Uninstall a system-installed app (from ARP/WMI) via winget or MSI.
                        /// </summary>
                        [RelayCommand]
                        internal async Task UninstallInstalledAppAsync(InstalledApp app, CancellationToken cancellationToken = default)
                        {
                            if (app is null) return;

                            var confirm = new ContentDialog
                            {
                                Title = _loc.Format("UninstallConfirmTitle", app.Name),
                                Content = _loc["UninstallConfirmWinget"],
                                PrimaryButtonText = _loc["MenuUninstall"],
                                CloseButtonText = _loc["Cancel"],
                                DefaultButton = ContentDialogButton.Close,
                                XamlRoot = App.MainWindow!.Content.XamlRoot,
                            };
                            if (await confirm.ShowAsync() != ContentDialogResult.Primary)
                                return;

                            // Try winget uninstall first
                                                        if (WingetAvailable)
                                                                                                                {
                                                                                                                    var (exitCode, output) = await _winget.UninstallAsync(app.Name, cancellationToken);
                                                                                                                    if (exitCode == 0)
                                                                                                                    {
                                                                                                                        StatusText = _loc.Format("UninstallDone", app.Name);
                                                                                                                        return;
                                                                                                                    }
                                                                                                                    StatusText = _loc["UninstallFailed"] + ": " + (output ?? "Unknown error");
                                                                                                                    return;
                                                                                                                }

                                                                                                                StatusText = _loc["UninstallFailed"] + ": winget not available";
                                                                                                                                        }

                                                                                                                            /// <summary>
                                                                                                                            /// Check updates for a single tracked app.
                                                                                                                            /// </summary>
                                                                                                                            [RelayCommand]
                                                                                                                            private async Task CheckSingleAppUpdateAsync(TrackedAppItem? item, CancellationToken cancellationToken = default)
                                                                                                                            {
                                                                                                                                if (item is null) return;
                                                                                                                                await CheckAppAsync(item, cancellationToken);
                                                                                                                            }

                                                                                                                            /// <summary>
                                                                                                                            /// Check updates for a single tracked app (extracted from CheckUpdatesAsync).
                                                                                                                            /// </summary>
                                                                                                                            private async Task CheckAppAsync(TrackedAppItem item, CancellationToken cancellationToken)
                                                                                                                            {
                                                                                                                                item.Stage = UpdateStage.Checking;
                                                                                                                                var result = await _engine.CheckAsync(item.App, cancellationToken);
                                                                                                                                BackgroundCheckOrder.RecordOutcome(item.App, result.Status);
                                                                                                                                item.StatusText = Describe(result.Status);
                                                                                                                                item.StatusDetail = result.Detail;
                                                                                                                                item.UpdateAvailable = result.Status == UpdateStatus.UpdateAvailable;
                                                                                                                                item.LatestTag = result.NewRelease?.TagName;
                                                                                                                                item.PendingNotes = result.NewRelease?.Body;
                                                                                                                                item.ReportVersionChanged();
                                                                                                                                item.Stage = result.Status switch
                                                                                                                                {
                                                                                                                                    UpdateStatus.Error => UpdateStage.Error,
                                                                                                                                    UpdateStatus.UpdateAvailable => UpdateStage.Idle,
                                                                                                                                    _ => UpdateStage.Done,
                                                                                                                                };
                                                                                                                                UpdatesAvailableCount = Apps.Count(a => a.UpdateAvailable);
                                                                                                                            }

                                                                                                                            /// <summary>
                                                                                                                            /// Remove an app from tracking (does NOT uninstall files, just stops monitoring).
                                                                                                                            /// </summary>
                                                                                                                            [RelayCommand]
                                                                                                                            private async Task RemoveTrackedAppAsync(TrackedAppItem? item)
                                                                                                                            {
                                                                                                                                if (item is null) return;

                                                                                                                                var confirm = new ContentDialog
                                                                                                                                {
                                                                                                                                    Title = _loc.Format("RemoveTrackedConfirmTitle", item.App.Name),
                                                                                                                                    Content = _loc["RemoveTrackedConfirm"],
                                                                                                                                    PrimaryButtonText = _loc["Remove"],
                                                                                                                                    CloseButtonText = _loc["Cancel"],
                                                                                                                                    DefaultButton = ContentDialogButton.Close,
                                                                                                                                    XamlRoot = App.MainWindow!.Content.XamlRoot,
                                                                                                                                };
                                                                                                                                if (await confirm.ShowAsync() != ContentDialogResult.Primary)
                                                                                                                                    return;

                                                                                                                                _config.TrackedApps.Remove(item.App);
                                                                                                                                _configStore.Save(_config);
                                                                                                                                Apps.Remove(item);
                                                                                                                                AppsTrackedCount = Apps.Count;
                                                                                                                                StatusText = _loc.Format("RemoveTrackedDone", item.App.Name);
                                                                                                                            }

                                                                                                                            /// <summary>
                                                                                                                            /// Layer-1 UI: app details dialog — versions, pending release notes, per-app
                                                                                                                            /// history, automation consent and actions. Primary = update, Secondary = rollback.
                                                                                                                            /// </summary>
                                                                                                                            [RelayCommand]
                                                                                                                            private async Task ShowAppDetailsAsync(TrackedAppItem? item)
                                                                                                                            {
                                                                                                                                if (item is null) return;

                                                                                                                                var appHistory = _history.Load()
            .Where(h => string.Equals(h.AppName, item.App.Name, StringComparison.OrdinalIgnoreCase))
            .TakeLast(5)
            .Reverse()
            .ToList();

        var dialog = new Views.AppDetailsDialog(item, appHistory, consent =>
        {
            item.App.AutoApplyConsent = consent;
            _configStore.Save(_config); // same instance the VM holds — persists the flag
        })
        {
            XamlRoot = App.MainWindow!.Content.XamlRoot,
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary && item.UpdateAvailable)
            await UpdateOneAsync(item, default);
        else if (result == ContentDialogResult.Secondary && item.RollbackAvailable)
            await RollbackAppAsync(item);
    }

    public async Task ShowOnboardingIfNeededAsync()
    {
        var cfg = _configStore.Load();
        if (!cfg.ShowOnboarding) return;

        var dialog = new ContentDialog
        {
            Title = _loc["OnboardingTitle"],
            Content = _loc["OnboardingText"],
            CloseButtonText = _loc["OnboardingOk"],
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = App.MainWindow!.Content.XamlRoot,
        };
        await dialog.ShowAsync();

        cfg.ShowOnboarding = false;
        _configStore.Save(cfg);
    }

private void ApplyCatalogFilter()
    {
        FilteredCatalogApps.Clear();
        var query = CatalogSearchQuery?.Trim() ?? "";
        // category gate applies to both paths (0 = all)
        var category = CatalogCategoryIndex > 0 && CatalogCategoryIndex <= CatalogCategories.Count
            ? CatalogCategories[CatalogCategoryIndex - 1]
            : null;

        if (query.Length == 0)
        {
            foreach (var item in CatalogApps)
            {
                if (category is null || item.Entry.Category == category)
                    FilteredCatalogApps.Add(item);
            }
            return;
        }

        var entries = CatalogApps.Select(i => i.Entry).ToList();
        var ranked = CatalogSearch.Rank(entries, query);
        foreach (var entry in ranked)
        {
            if (category is not null && entry.Category != category)
                continue;
            var vm = CatalogApps.First(c => ReferenceEquals(c.Entry, entry));
            FilteredCatalogApps.Add(vm);
        }
    }

    // === W1–W3: winget search & install ("Установить из winget") ===

    [ObservableProperty]
    public partial string WingetSearchQuery { get; set; } = "";

    [ObservableProperty]
    public partial bool IsWingetSearching { get; set; }

    public ObservableCollection<WingetSearchItem> WingetSearchResults { get; } = [];

    /// <summary>True after a search that returned nothing (drives the "no results" hint).</summary>
    [ObservableProperty]
    public partial bool WingetSearchNoResults { get; set; }

    [RelayCommand]
    private async Task SearchWingetAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(WingetSearchQuery)) return;
        IsWingetSearching = true;
        WingetSearchNoResults = false;
        StatusText = _loc["wingetSearching"];
        try
        {
            var results = await _winget.SearchAsync(WingetSearchQuery, cancellationToken);
            WingetSearchResults.Clear();
            foreach (var r in results)
                WingetSearchResults.Add(new WingetSearchItem(r));
            WingetSearchNoResults = results.Count == 0;
            StatusText = results.Count == 0 ? _loc["wingetSearchNoResults"] : _loc["checkDone"];
        }
        catch (Exception ex)
        {
            StatusText = $"{_loc["error"]}: {ex.Message}";
        }
        finally
        {
            IsWingetSearching = false;
        }
    }

    [RelayCommand]
    private async Task InstallWingetPackageAsync(WingetSearchItem? item, CancellationToken cancellationToken)
    {
        if (item is null || item.IsInstalling || item.Installed) return;
        item.IsInstalling = true;
        item.StatusText = _loc["wingetInstalling"];
        StatusText = _loc.Format("updating", item.Name);
        try
        {
            var (exitCode, output) = await _winget.InstallAsync(item.Id, cancellationToken);
            if (exitCode == 0 || exitCode == Infrastructure.WingetService.AlreadyInstalledExitCode)
            {
                // W3: track the package so the existing update engine keeps it fresh
                if (_config.TrackedApps.All(a => a.WingetPackageId != item.Id))
                {
                    var app = new TrackedApp
                    {
                        Name = item.Name,
                        // winget packages have no GitHub repo; the winget path never
                        // touches RepoOwner/RepoName (CheckAsync short-circuits on kind)
                        RepoOwner = "",
                        RepoName = "",
                        UpdateKind = UpdateKind.WingetPackage,
                        WingetPackageId = item.Id,
                        CurrentVersion = item.Version,
                        Enabled = true,
                        IgnorePreReleases = true,
                    };
                    _config.TrackedApps.Add(app);
                    _configStore.Save(_config);
                    Apps.Add(new TrackedAppItem(app));
                    AppsTrackedCount = Apps.Count;
                }
                item.Installed = true;
                item.StatusText = exitCode == 0
                    ? _loc["wingetInstalled"]
                    : _loc["wingetAlreadyInstalled"];
                StatusText = item.StatusText;
            }
            else
            {
                item.StatusText = $"{_loc["wingetInstallFailed"]}: {WingetOutputParser.Squash(output)}";
                StatusText = item.StatusText;
            }
        }
        catch (Exception ex)
        {
            item.StatusText = $"{_loc["wingetInstallFailed"]}: {ex.Message}";
            StatusText = item.StatusText;
        }
        finally
        {
            item.IsInstalling = false;
        }
    }

    [RelayCommand]
    private async Task CheckUpdatesAsync(CancellationToken cancellationToken)
    {
        StatusText = _loc["Checking"];
        // B9: known-pending updates first — same priority as the background cycle
        foreach (var item in BackgroundCheckOrder.Prioritize(Apps.Select(a => a.App)))
        {
            // map back to the item VM (Apps order preserved)
            var vm = Apps.FirstOrDefault(a => ReferenceEquals(a.App, item));
            if (vm is null) continue;

            vm.Stage = UpdateStage.Checking;
            var result = await _engine.CheckAsync(item, cancellationToken);

            // B9+P6: persist the verdict — drives the next background cycle's priority and error backoff
            BackgroundCheckOrder.RecordOutcome(item, result.Status);

            vm.StatusText = Describe(result.Status);
            vm.StatusDetail = result.Detail; // D5: error/decision reason for the card tooltip
            vm.UpdateAvailable = result.Status == UpdateStatus.UpdateAvailable;
            vm.LatestTag = result.NewRelease?.TagName; // A1: for "skip this version"
            vm.PendingNotes = result.NewRelease?.Body; // Layer-1: release notes
            vm.ReportVersionChanged(); // P3: auto-detect may have updated CurrentVersion during the check
            vm.Stage = result.Status switch
            {
                UpdateStatus.Error => UpdateStage.Error,
                UpdateStatus.UpdateAvailable => UpdateStage.Idle,
                _ => UpdateStage.Done,
            };
        }
        UpdatesAvailableCount = Apps.Count(a => a.UpdateAvailable);
        _config.LastCheckUtc = DateTimeOffset.UtcNow; // footer: «Последняя проверка: …»
        _configStore.Save(_config);   // B9: persist LastCheckStatus for background priority
        StatusText = _loc["CheckDone"];
    }

    [RelayCommand]
        private async Task UpdateAllAsync(CancellationToken cancellationToken)
        {
            _updateAllCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            IsUpdateAllRunning = true;

            // A5: batch summary counters — one balloon with the outcome, not a toast per app
            var okCount = 0;
            var failedNames = new List<string>();

            try
            {
                foreach (var item in Apps.Where(a => a.App.UpdateKind != UpdateKind.WingetPackage))
                {
                    if (_updateAllCts.Token.IsCancellationRequested)
                        break;

                    item.Stage = UpdateStage.Checking;
                    var check = await _engine.CheckAsync(item.App, _updateAllCts.Token);
                    item.StatusText = Describe(check.Status);
                    item.StatusDetail = check.Detail; // D5
                    item.UpdateAvailable = check.Status == UpdateStatus.UpdateAvailable;
                    item.LatestTag = check.NewRelease?.TagName; // A1
                    item.PendingNotes = check.NewRelease?.Body; // Layer-1: release notes
                    if (check.Status != UpdateStatus.UpdateAvailable)
                    {
                        item.Stage = check.Status == UpdateStatus.Error ? UpdateStage.Error : UpdateStage.Done;
                        continue;
                    }

                    StatusText = _loc.Format("Updating", item.Name);
                    item.Stage = UpdateStage.Downloading;
                    item.ProgressValue = 0;
                    // same live percent+speed label as the single-update flow
                    var totalBytes = check.ChosenAsset?.Size ?? 0;
                    var lastPct = 0.0;
                    var lastAt = DateTimeOffset.UtcNow;
                    var download = new Progress<double>(p =>
                    {
                        item.ProgressValue = p;
                        var now = DateTimeOffset.UtcNow;
                        var dt = (now - lastAt).TotalSeconds;
                        if (totalBytes > 0 && p > lastPct && dt >= 0.4)
                        {
                            var bytesPerSec = (p - lastPct) / 100.0 * totalBytes / dt;
                            item.StatusText = $"{_loc["StageDownloading"]} {(int)p}% · {FormatSpeed(bytesPerSec)}";
                            lastPct = p;
                            lastAt = now;
                        }
                    });
                    var history = await _engine.ApplyAsync(item.App, check, download, _updateAllCts.Token);
                    _history.Append(history);
                    if (history.Success)
                    {
                        item.Stage = UpdateStage.Done;
                        item.UpdateAvailable = false;
                        item.Refresh(); // raises Version AND RollbackAvailable (B10 button appears after first update)
                        UpdatedTodayCount++;
                        okCount++;
                    }
                    else
                    {
                        item.Stage = UpdateStage.Error;
                        item.StatusDetail = UpdateErrorHints.WithHint(history.Detail, k => _loc[k]); // D5
                        failedNames.Add(item.Name);
                    }
                    item.StatusText = history.Success ? _loc["Updated"] : _loc["UpdateFailed"];
                }
            }
            finally
            {
                IsUpdateAllRunning = false;
                _updateAllCts?.Dispose();
                _updateAllCts = null;
            }

            UpdatesAvailableCount = Apps.Count(a => a.UpdateAvailable);
            _configStore.Save(_config);
            StatusText = _loc["CheckDone"];

            // A5: tray balloon with the batch outcome (only when something actually ran)
            if (okCount > 0 || failedNames.Count > 0)
            {
                var summary = failedNames.Count == 0
                    ? string.Format(_loc["BatchSummaryOk"], okCount)
                    : string.Format(_loc["BatchSummaryMixed"], okCount, failedNames.Count,
                        string.Join(", ", failedNames));
                try { App.Resolve<AppWatch.Platform.TrayService>().ShowBalloon("AppWatch", summary); }
                catch { /* tray not ready — ignore */ }
            }
        }

        [RelayCommand]
        private void StopUpdateAll()
        {
            _updateAllCts?.Cancel();
        }

    [RelayCommand]
    private async Task UpdateOneAsync(TrackedAppItem? item, CancellationToken cancellationToken)
    {
        if (item is null) return;

        StatusText = _loc.Format("updating", item.Name);
        item.Stage = UpdateStage.Checking;
        var check = await _engine.CheckAsync(item.App, cancellationToken);
        item.StatusText = Describe(check.Status);
        item.StatusDetail = check.Detail; // D5
        if (check.Status != UpdateStatus.UpdateAvailable)
        {
            item.Stage = check.Status == UpdateStatus.Error ? UpdateStage.Error : UpdateStage.Done;
            return;
        }

        item.Stage = UpdateStage.Downloading;
        item.ProgressValue = 0;
        // live "Скачивание 45% · 3.2 МБ/с": percent from the engine (bytes),
        // speed computed from percent deltas over the asset size.
        var totalBytes = check.ChosenAsset?.Size ?? 0;
        var lastPct = 0.0;
        var lastAt = DateTimeOffset.UtcNow;
        var download = new Progress<double>(p =>
        {
            item.ProgressValue = p;
            var now = DateTimeOffset.UtcNow;
            var dt = (now - lastAt).TotalSeconds;
            if (totalBytes > 0 && p > lastPct && dt >= 0.4)
            {
                var bytesPerSec = (p - lastPct) / 100.0 * totalBytes / dt;
                item.StatusText = $"{_loc["StageDownloading"]} {(int)p}% · {FormatSpeed(bytesPerSec)}";
                lastPct = p;
                lastAt = now;
            }
        });
        var history = await _engine.ApplyAsync(item.App, check, download, cancellationToken);
        _history.Append(history);
        if (history.Success)
        {
            item.Stage = UpdateStage.Done;
            item.UpdateAvailable = false;
            item.Refresh(); // raises Version AND RollbackAvailable (B10 button appears after first update)
            UpdatedTodayCount++;
        }
        else
        {
            item.Stage = UpdateStage.Error;
        }
        item.StatusDetail = history.Success ? check.Detail : UpdateErrorHints.WithHint(history.Detail, k => _loc[k]); // D5: failure reason + hint in tooltip
        item.StatusText = history.Success ? _loc["updated"] : _loc["updateFailed"];
        UpdatesAvailableCount = Apps.Count(a => a.UpdateAvailable);
        _configStore.Save(_config);
        StatusText = _loc["checkDone"];
    }

    private static string FormatSpeed(double bytesPerSec) => bytesPerSec switch
    {
        >= 1024 * 1024 => $"{bytesPerSec / (1024 * 1024):F1} МБ/с",
        >= 1024 => $"{bytesPerSec / 1024:F0} КБ/с",
        _ => $"{bytesPerSec:F0} Б/с",
    };

    private static string FormatBytes(long bytes) => bytes switch
    {
        >= 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024 * 1024):F1} ГБ",
        >= 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1} МБ",
        >= 1024 => $"{bytes / 1024.0:F0} КБ",
        _ => $"{bytes} Б",
    };

    [RelayCommand]
    private void LaunchApp(TrackedAppItem? item)
    {
        if (item?.App.InstallDirectory is null || item.App.ExecutableName is null) return;
        var path = Path.Combine(item.App.InstallDirectory, item.App.ExecutableName);
        // P0 follow-up: launching a non-existent exe (e.g. installer-kind left our
        // install dir empty) crashed with a raw Win32Exception into the debugger —
        // guard with a clear status instead.
        if (!File.Exists(path))
        {
            StatusText = _loc.Format("launchFileMissing", path);
            return;
        }
        _launcher.Launch(path);
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
            catch (WingetCatalogBrokenException)
            {
                // Known Microsoft-side catalog bug (winget-cli#6613): winget dies for everyone.
                // Scoped ops (search / install --id) still work — say it plainly, not "error".
                Log.Warning("winget catalog is broken (E_INVALIDARG, winget-cli#6613)");
                StatusText = _loc["WingetCatalogBroken"];
                WingetTotalCount = 0;
                WingetUpdatesAvailable = 0;
                WingetPendingUpdates = 0;
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

        var total = selected.Count;
        var updatedNames = new List<string>();
        var failedNames = new List<string>();

        // Mark all as "queued" so the user sees the full picture before anything starts
        for (var i = 0; i < selected.Count; i++)
            selected[i].Status = _loc.Format("WingetQueued", i + 1, total);

        for (var i = 0; i < selected.Count; i++)
        {
            var item = selected[i];
            StatusText = _loc.Format("WingetUpdating", item.Name, i + 1, total); // main bar: "Обновление Telegram (1/3)…"
            item.Status = _loc["WingetStarting"];
            item.ProgressValue = 0;
            item.ElapsedText = "";
            item.IsUpdating = true;

            // honest live feedback: winget gives no percents (redirected stdout),
            // so show real phases + a running clock instead of a dead bar.
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var ticker = new Microsoft.UI.Xaml.DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            ticker.Tick += (_, _) => item.ElapsedText = FormatElapsed(stopwatch.Elapsed);
            ticker.Start();

            var progress = new Progress<AppWatch.Core.Domain.WingetProgress>(p =>
            {
                item.Status = p.Phase switch
                {
                    AppWatch.Core.Domain.WingetPhase.Downloading => p.Bytes is { } bytes
                        ? $"{_loc["WingetPhDownload"]} {FormatBytes(bytes)} · {FormatSpeed(p.BytesPerSec ?? 0)}"
                        : _loc["WingetPhDownload"],
                    AppWatch.Core.Domain.WingetPhase.Verifying => _loc["WingetPhVerify"],
                    AppWatch.Core.Domain.WingetPhase.Downloaded => _loc["WingetPhDownloaded"],
                    AppWatch.Core.Domain.WingetPhase.Installing => _loc["WingetPhInstall"],
                    _ => item.Status,
                };
            });
            var (ok, message) = await _winget.UpgradePackageAsync(item.Id, item.Name, progress, cancellationToken);
            ticker.Stop();
            var detailed = ok ? null : UpdateErrorHints.WithHint(message, k => _loc[k]);
            _history.Append(new UpdateHistoryEntry(
                item.Name, item.Row.Update.CurrentVersion, item.Row.Update.AvailableVersion,
                DateTimeOffset.UtcNow, ok, detailed));
            item.IsUpdating = false;
            item.ElapsedText = ok ? FormatElapsed(stopwatch.Elapsed) : "";
            item.ProgressValue = ok ? 100 : item.ProgressValue;
            item.Status = ok ? _loc["WingetDone"] : $"{_loc["WingetFailed"]}: {detailed}";
            if (ok) updatedNames.Add(item.Name); else failedNames.Add($"{item.Name} — {detailed}");
        }
        StatusText = _loc["WingetBatchDone"];

        // Summary dialog: what updated, what failed, then re-scan on close
        await ShowWingetBatchSummaryAsync(updatedNames, failedNames);
    }

    private static string FormatElapsed(TimeSpan t) =>
        t.TotalHours >= 1 ? $"{(int)t.TotalHours}:{t.Minutes:D2}:{t.Seconds:D2}" : $"{(int)t.TotalMinutes}:{t.Seconds:D2}";

    /// <summary>
    /// Post-batch summary dialog (C# ContentDialog — XAML ContentDialog crashes the compiler
    /// on RU locale, see AddEditAppDialog). On close, re-scans the winget updates list.
    /// </summary>
    private async Task ShowWingetBatchSummaryAsync(List<string> updated, List<string> failed)
    {
        var loc = AppWatch.Localization.Loc.Instance;
        var dialog = new ContentDialog
        {
            XamlRoot = App.MainWindow!.Content.XamlRoot,
            Title = updated.Count > 0 && failed.Count == 0
                ? string.Format(loc["WingetSummaryOkTitle"], updated.Count)
                : string.Format(loc["WingetSummaryMixedTitle"], updated.Count, failed.Count),
            PrimaryButtonText = loc["WingetSummaryClose"],
            DefaultButton = ContentDialogButton.Primary,
            MinWidth = 440,
        };

        var panel = new StackPanel { Spacing = 8, Padding = new Thickness(0, 8, 0, 0) };

        if (updated.Count > 0)
        {
            panel.Children.Add(new TextBlock
            {
                Text = loc["WingetSummaryUpdated"],
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                FontSize = 13,
            });
            foreach (var name in updated)
                panel.Children.Add(new TextBlock
                {
                    Text = $"✓  {name}",
                    FontSize = 13,
                    Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                        Microsoft.UI.ColorHelper.FromArgb(255, 0x2E, 0xA0, 0x43)),
                    Margin = new Thickness(12, 0, 0, 0),
                });
        }

        if (failed.Count > 0)
        {
            panel.Children.Add(new TextBlock
            {
                Text = loc["WingetSummaryFailed"],
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                FontSize = 13,
                Margin = new Thickness(0, updated.Count > 0 ? 12 : 0, 0, 0),
            });
            foreach (var name in failed)
                panel.Children.Add(new TextBlock
                {
                    Text = $"✗  {name}",
                    FontSize = 13,
                    Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                        Microsoft.UI.ColorHelper.FromArgb(255, 0xD8, 0x3B, 0x01)),
                    Margin = new Thickness(12, 0, 0, 0),
                    TextWrapping = TextWrapping.Wrap,
                });
        }

        dialog.Content = new ScrollViewer
        {
            Content = panel,
            MaxHeight = 380,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };

        await dialog.ShowAsync();

        // Re-scan the updates list — the old entries are stale after the batch
        await ScanWingetAsync(default);
    }

    /// <summary>
    /// Startup visibility: winget scan (local, free) + tracked-apps check a few seconds
    /// after launch, so the user SEES fresh statuses on the cards instead of stale ones.
    /// App check is gated by AutoCheckEnabled (same consent as the background monitor);
    /// the 15-min GitHub response cache keeps the monitor's own 30s cycle from double-burning quota.
    /// </summary>
        public async Task RunStartupTasksAsync()
        {
            try
            {
                // UPD-02: crash recovery first — a leftover journal means the last
                // update died mid-apply; restore before anything else touches files.
                await RecoverInterruptedUpdateAsync();

                // Small delay to let the UI render fully before background work
                await Task.Delay(TimeSpan.FromSeconds(1));

                if (WingetAvailable)
                    await ScanWingetAsync(default);

                if (_config.AutoCheckEnabled && Apps.Count > 0)
                {
                    StatusText = _loc["checking"];
                    await RunBackgroundCheckAsync();
                    StatusText = _loc["checkDone"];
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "startup check failed");
            }
        }

    /// <summary>
    /// UPD-02: detects an update interrupted by a hard kill (power loss, crash,
    /// taskkill) and restores the ".old" backup so the app is never left in a
    /// half-replaced state. Pure decision in PendingUpdatePolicy, file IO here.
    /// </summary>
    private async Task RecoverInterruptedUpdateAsync()
    {
        var pending = _journal.Load();
        if (pending is null) return;

        Log.Warning("interrupted update detected: {App} → {Version} (started {Started:HH:mm})",
            pending.AppName, pending.ToVersion, pending.StartedUtc);

        var backupExists = Directory.Exists(pending.InstallDirectory + ".old");
        var restored = false;
        if (PendingUpdatePolicy.Decide(backupExists) == PendingUpdatePolicy.Action.RestoreBackup)
        {
            try
            {
                _files.RestoreBackup(pending.InstallDirectory);
                restored = true;
                Log.Information("restored previous version of {App} from backup", pending.AppName);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "restore after interrupted update failed for {App}", pending.AppName);
            }
        }

        _journal.Clear();
        var detail = restored
            ? $"update interrupted — previous version restored ({pending.FromVersion ?? "?"})"
            : "update interrupted — no changes had been applied";
        _history.Append(new UpdateHistoryEntry(
            pending.AppName, pending.FromVersion ?? "?", pending.ToVersion,
            DateTimeOffset.UtcNow, false, detail));
        StatusText = pending.AppName + ": " + _loc["InterruptedUpdateRecovered"]
            + (restored ? _loc["InterruptedRestored"] : _loc["InterruptedNoChanges"]);
        await Task.CompletedTask;
    }

    /// <summary>
    /// Silent check + auto-apply for scheduled background runs (called by the App timer).
    /// </summary>
    public async Task RunBackgroundCheckAsync()
    {
        // reviewer: sync the consent flag from disk — the ctor-loaded snapshot would both
        // miss a hand-edited config.json and then clobber it on the trailing Save below.
        // Sync the FIELD only: replacing _config wholesale would break the reference
        // identity between Apps items and _config.TrackedApps.
        _config.AutoApplyUpdates = _configStore.Load().AutoApplyUpdates;

        foreach (var item in Apps.Where(a => a.App.UpdateKind != UpdateKind.WingetPackage && a.App.Enabled))
        {
            item.Stage = UpdateStage.Checking;
            var check = await _engine.CheckAsync(item.App);
            item.StatusText = Describe(check.Status);
            item.UpdateAvailable = check.Status == UpdateStatus.UpdateAvailable;
            item.LatestTag = check.NewRelease?.TagName; // A1: for "skip this version"
            item.PendingNotes = check.NewRelease?.Body; // Layer-1: release notes
            item.StatusDetail = check.Detail; // reviewer: stale-detail cleanup on every plain check
            if (check.Status != UpdateStatus.UpdateAvailable)
            {
                item.Stage = check.Status == UpdateStatus.Error ? UpdateStage.Error : UpdateStage.Done;
                continue;
            }

            // P1: consent gate — never auto-apply without explicit user consent.
            // The card already shows "Доступно обновление" + UpdateAvailable=true,
            // so the update is surfaced, not silently applied.
            if (!UpdatePolicy.ShouldAutoApply(_config, item.App))
            {
                // reviewer: same convention as CheckUpdatesAsync — update found, awaiting user action
                item.Stage = UpdateStage.Idle;
                continue;
            }

            item.StatusText = _loc.Format("updating", item.Name);
            item.Stage = UpdateStage.Downloading;
            item.ProgressValue = 0;
            var download = new Progress<double>(p => item.ProgressValue = p);
            var history = await _engine.ApplyAsync(item.App, check, download);
            _history.Append(history);
            if (history.Success)
            {
                item.Stage = UpdateStage.Done;
                item.UpdateAvailable = false;
                item.Refresh(); // raises Version AND RollbackAvailable (B10 button appears after first update)
                UpdatedTodayCount++;
            }
            else
            {
                item.Stage = UpdateStage.Error;
            }
            item.StatusDetail = history.Success ? null : history.Detail; // D5
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
