using System.ComponentModel;

namespace AppWatch.Localization;

/// <summary>
/// XAML-friendly localization wrapper for x:Bind: typed properties re-evaluate
/// on language switch (INotifyPropertyChanged with an empty property name).
/// </summary>
public sealed class Loc : INotifyPropertyChanged
{
    public static Loc Instance { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    private Loc()
    {
        LocalizationService.Instance.PropertyChanged += (_, _) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
    }

    public string this[string key] => LocalizationService.Instance[key];

    public string Format(string key, params object[] args) => LocalizationService.Instance.Format(key, args);

    public string WindowTitle => LocalizationService.Instance["windowTitle"];
            public string CheckUpdates => LocalizationService.Instance["checkUpdates"];
            public string UpdateAll => LocalizationService.Instance["updateAll"];
            public string Stop => LocalizationService.Instance["Stop"];
            public string BtnStop => LocalizationService.Instance["BtnStop"];
        public string Browse => this["Browse"];
            public string AddApp => LocalizationService.Instance["AddApp"];
        public string CatalogSearchPlaceholder => LocalizationService.Instance["CatalogSearchPlaceholder"];
                public string SourcesSection => LocalizationService.Instance["SourcesSection"];
                public string SourceNamePlaceholder => LocalizationService.Instance["SourceNamePlaceholder"];
                public string SourceUrlPlaceholder => LocalizationService.Instance["SourceUrlPlaceholder"];
                public string BtnAddSource => LocalizationService.Instance["BtnAddSource"];
                public string ImportFromStarsTitle => LocalizationService.Instance["ImportFromStarsTitle"];
                public string ImportStarsUsernameHint => LocalizationService.Instance["ImportStarsUsernameHint"];
                public string BtnLoadStars => LocalizationService.Instance["BtnLoadStars"];
                public string BtnImport => LocalizationService.Instance["BtnImport"];
                public string ImportStarsNothingSelected => LocalizationService.Instance["ImportStarsNothingSelected"];
                        public string ImportStarsDone => LocalizationService.Instance["ImportStarsDone"];
                        public string ImportStarsNoneAdded => LocalizationService.Instance["ImportStarsNoneAdded"];
                        public string TrackedApps => LocalizationService.Instance["trackedApps"];

        public string NoTrackedApps => LocalizationService.Instance["NoTrackedApps"];
            public string Catalog => LocalizationService.Instance["catalog"];
                    public string CatalogHint => LocalizationService.Instance["catalogHint"];
                    public string CatalogEmpty => LocalizationService.Instance["CatalogEmpty"];
            public string WingetUpdates => LocalizationService.Instance["wingetUpdates"];
                public string WingetGoTo => LocalizationService.Instance["wingetGoTo"];
                public string WingetUnavailable => LocalizationService.Instance["wingetUnavailable"];
                public string NavApps => LocalizationService.Instance["navApps"];
    public string NavCatalog => LocalizationService.Instance["navCatalog"];
    public string NavWinget => LocalizationService.Instance["navWinget"];
    public string NavSettings => LocalizationService.Instance["navSettings"];
    public string NavAbout => LocalizationService.Instance["navAbout"];
    public string TabHome => LocalizationService.Instance["TabHome"];
    public string NavHomeCatalog => LocalizationService.Instance["NavHomeCatalog"];
    public string TabCatalog => LocalizationService.Instance["TabCatalog"];
        public string TabUpdates => this["TabUpdates"];
        public string TabInstalledApps => this["TabInstalledApps"];
    public string SourceEnabled => LocalizationService.Instance["SourceEnabled"];
    public string SourceDisabled => LocalizationService.Instance["SourceDisabled"];
    public string BtnRemoveSource => LocalizationService.Instance["BtnRemoveSource"];
    public string InstalledSearchPlaceholder => LocalizationService.Instance["InstalledSearchPlaceholder"];
    public string InstalledCount => LocalizationService.Instance["InstalledCount"];
        public string NavHome => LocalizationService.Instance["NavHome"];
            public string BtnClearMyCatalog => LocalizationService.Instance["BtnClearMyCatalog"];
            public string SettingsCatalogSection => LocalizationService.Instance["SettingsCatalogSection"];
            public string AppsPageSubtitle => LocalizationService.Instance["appsPageSubtitle"];
            public string CatalogPageSubtitle => LocalizationService.Instance["catalogPageSubtitle"];
            public string WingetPageSubtitle => LocalizationService.Instance["wingetPageSubtitle"];
            public string SettingsPageTitle => LocalizationService.Instance["settingsPageTitle"];
            public string SettingsPageSubtitle => LocalizationService.Instance["settingsPageSubtitle"];
            public string AboutPageTitle => LocalizationService.Instance["aboutPageTitle"];
            public string AboutPageSubtitle => LocalizationService.Instance["aboutPageSubtitle"];

            // Settings page keys
                                public string SettingsGeneral => LocalizationService.Instance["SettingsGeneral"];
                                public string SettingsBehavior => LocalizationService.Instance["SettingsBehavior"];
                                public string SettingsGitHub => LocalizationService.Instance["SettingsGitHub"];
                                public string SettingsLanguage => LocalizationService.Instance["SettingsLanguage"];
                                public string SettingsTheme => LocalizationService.Instance["SettingsTheme"];
                                public string SettingsThemeSystem => LocalizationService.Instance["SettingsThemeSystem"];
                                public string SettingsThemeDark => LocalizationService.Instance["SettingsThemeDark"];
                                public string SettingsThemeLight => LocalizationService.Instance["SettingsThemeLight"];
                                public string SettingsAutoCheck => LocalizationService.Instance["SettingsAutoCheck"];
                                public string SettingsAutoCheckHint => LocalizationService.Instance["SettingsAutoCheckHint"];
                                public string SettingsInterval => LocalizationService.Instance["SettingsInterval"];
                                public string SettingsHours => LocalizationService.Instance["SettingsHours"];
                                public string SettingsPatToken => LocalizationService.Instance["SettingsPatToken"];
                                public string SettingsPatHint => LocalizationService.Instance["SettingsPatHint"];
                                public string SettingsPatStatusOk => LocalizationService.Instance["SettingsPatStatusOk"];
                                public string SettingsPatStatusEmpty => LocalizationService.Instance["SettingsPatStatusEmpty"];
                                public string SettingsSave => LocalizationService.Instance["SettingsSave"];
                                public string SettingsSaved => LocalizationService.Instance["SettingsSaved"];
                                public string SettingsSelfUpdate => LocalizationService.Instance["SettingsSelfUpdate"];
                                public string SettingsSelfUpdateHint => LocalizationService.Instance["SettingsSelfUpdateHint"];
                                public string SettingsScheduleSection => LocalizationService.Instance["SettingsScheduleSection"];
                                public string SettingsNextBackgroundCheck => LocalizationService.Instance["SettingsNextBackgroundCheck"];
                                public string SettingsNextCatalogCheck => LocalizationService.Instance["SettingsNextCatalogCheck"];
                                public string SettingsScheduleNone => LocalizationService.Instance["SettingsScheduleNone"];
                                public string SettingsExportImport => LocalizationService.Instance["SettingsExportImport"];
                                public string ExportApps => LocalizationService.Instance["ExportApps"];
                                public string ImportApps => LocalizationService.Instance["ImportApps"];
                                public string CopyAppsList => LocalizationService.Instance["CopyAppsList"];
                                public string SettingsSelfUpdateCheck => LocalizationService.Instance["SettingsSelfUpdateCheck"];
                                public string SettingsSelfUpdateApply => LocalizationService.Instance["SettingsSelfUpdateApply"];
                                public string SettingsNoUpdates => LocalizationService.Instance["SettingsNoUpdates"];
                                public string SettingsUpdatesFound => LocalizationService.Instance["SettingsUpdatesFound"];
                                public string SettingsSelfUpdateApplying => LocalizationService.Instance["SettingsSelfUpdateApplying"];
                                public string SettingsSelfUpdateApplied => LocalizationService.Instance["SettingsSelfUpdateApplied"];
                                public string SettingsSelfUpdateFailed => LocalizationService.Instance["SettingsSelfUpdateFailed"];
                                public string SettingsUpdateStrategy => LocalizationService.Instance["SettingsUpdateStrategy"];
                                public string SettingsUpdateStrategyHint => LocalizationService.Instance["SettingsUpdateStrategyHint"];
                                public string SettingsUpdateStrategyAuto => LocalizationService.Instance["SettingsUpdateStrategyAuto"];
                                public string SettingsUpdateStrategyBuiltIn => LocalizationService.Instance["SettingsUpdateStrategyBuiltIn"];
                                public string SettingsUpdateStrategySilent => LocalizationService.Instance["SettingsUpdateStrategySilent"];
                                public string SettingsUpdateStrategyPortable => LocalizationService.Instance["SettingsUpdateStrategyPortable"];
                                public string SettingsUpdateStrategyExternal => LocalizationService.Instance["SettingsUpdateStrategyExternal"];
                                public string SettingsUpdateStrategyManual => LocalizationService.Instance["SettingsUpdateStrategyManual"];

                                // Add/Edit App dialog keys
                                public string AddAppTitle => LocalizationService.Instance["AddAppTitle"];
                                public string EditAppTitle => LocalizationService.Instance["EditAppTitle"];
                                public string BtnAdd => LocalizationService.Instance["BtnAdd"];
                                public string BtnSave => LocalizationService.Instance["BtnSave"];
                                public string AppName => LocalizationService.Instance["AppName"];
                                public string AppNamePlaceholder => LocalizationService.Instance["AppNamePlaceholder"];
                                public string RepoOwner => LocalizationService.Instance["RepoOwner"];
                                public string RepoName => LocalizationService.Instance["RepoName"];
        public string FetchFromGitHub => LocalizationService.Instance["FetchFromGitHub"];
        public string OpenInGitHub => LocalizationService.Instance["OpenInGitHub"];
        public string RepoOwnerNameRequired => LocalizationService.Instance["RepoOwnerNameRequired"];
        public string RepoNotFound => LocalizationService.Instance["RepoNotFound"];
        public string RepoFetchFailed => LocalizationService.Instance["RepoFetchFailed"];
                                public string UpdateKind => LocalizationService.Instance["UpdateKind"];
                                public string UpdateKindPortableZip => LocalizationService.Instance["UpdateKindPortableZip"];
                                public string UpdateKindInstaller => LocalizationService.Instance["UpdateKindInstaller"];
                                public string UpdateKindWingetPackage => LocalizationService.Instance["UpdateKindWingetPackage"];
                                public string WingetSearchSection => LocalizationService.Instance["wingetSearchSection"];
                                public string WingetSearchPlaceholder => LocalizationService.Instance["wingetSearchPlaceholder"];
                                public string WingetSearch => LocalizationService.Instance["wingetSearch"];
                                public string WingetSearchNoResults => LocalizationService.Instance["wingetSearchNoResults"];
                                                        public string UpdateStrategy => LocalizationService.Instance["UpdateStrategy"];
                                                        public string UpdateStrategyAuto => LocalizationService.Instance["SettingsUpdateStrategyAuto"];
                                                        public string UpdateStrategyBuiltIn => LocalizationService.Instance["SettingsUpdateStrategyBuiltIn"];
                                                        public string UpdateStrategySilent => LocalizationService.Instance["SettingsUpdateStrategySilent"];
                                                        public string UpdateStrategyPortable => LocalizationService.Instance["SettingsUpdateStrategyPortable"];
                                                        public string UpdateStrategyExternal => LocalizationService.Instance["SettingsUpdateStrategyExternal"];
                                                        public string ExecutableName => LocalizationService.Instance["ExecutableName"];
                                public string ProcessName => LocalizationService.Instance["ProcessName"];
                                public string InstallDirectory => LocalizationService.Instance["InstallDirectory"];
                                public string InstallDirectoryPlaceholder => LocalizationService.Instance["InstallDirectoryPlaceholder"];
                                public string WingetPackageId => LocalizationService.Instance["WingetPackageId"];
                                public string LaunchAfterUpdate => LocalizationService.Instance["LaunchAfterUpdate"];
                                public string IgnorePreReleases => LocalizationService.Instance["IgnorePreReleases"];
                                public string AutoDetectVersion => LocalizationService.Instance["AutoDetectVersion"];
                                                                public string Cancel => LocalizationService.Instance["Cancel"];
                                                                public string NavHistory => LocalizationService.Instance["NavHistory"];
                                public string SourceGitHub => LocalizationService.Instance["SourceGitHub"];
                                public string SourceWinget => LocalizationService.Instance["SourceWinget"];
                                public string SourceInstaller => LocalizationService.Instance["SourceInstaller"];
                                public string SourceExternal => LocalizationService.Instance["SourceExternal"];
                                public string RemoveTrackedConfirm => LocalizationService.Instance["RemoveTrackedConfirm"];
                                public string RemoveTrackedConfirmTitle => LocalizationService.Instance["RemoveTrackedConfirmTitle"];
                                public string RemoveTrackedDone => LocalizationService.Instance["RemoveTrackedDone"];
                                public string Remove => LocalizationService.Instance["Remove"];
                                                                public string HistoryPageTitle => LocalizationService.Instance["HistoryPageTitle"];
                                                                public string HistoryPageSubtitle => LocalizationService.Instance["HistoryPageSubtitle"];
                                                                public string HistorySection => LocalizationService.Instance["HistorySection"];
                                                                public string HistoryEmpty => LocalizationService.Instance["HistoryEmpty"];
                                                                public string HistorySuccess => LocalizationService.Instance["HistorySuccess"];
                                                                public string HistoryFailed => LocalizationService.Instance["HistoryFailed"];

                                // About page keys
                                public string AboutVersion => LocalizationService.Instance["AboutVersion"];
        public string AboutRepo => LocalizationService.Instance["AboutRepo"];
        public string AboutAttribution => LocalizationService.Instance["AboutAttribution"];

        // Hero banner keys
        public string HeroTitle => LocalizationService.Instance["HeroTitle"];
        public string HeroMetricTracked => LocalizationService.Instance["HeroMetricTracked"];
        public string HeroMetricUpdates => LocalizationService.Instance["HeroMetricUpdates"];
        public string HeroMetricUpdatedToday => LocalizationService.Instance["HeroMetricUpdatedToday"];

        // Quick actions
                public string QuickCheck => LocalizationService.Instance["QuickCheck"];
                public string QuickStatus => LocalizationService.Instance["QuickStatus"];
                public string BtnRefresh => LocalizationService.Instance["BtnRefresh"];
                public string NoUpdatesAvailable => LocalizationService.Instance["NoUpdatesAvailable"];
                public string NoInstalledApps => LocalizationService.Instance["NoInstalledApps"];

                // Stage texts
        public string StageChecking => LocalizationService.Instance["StageChecking"];
        public string StageDownloading => LocalizationService.Instance["StageDownloading"];
        public string StageReplacing => LocalizationService.Instance["StageReplacing"];
        public string StageRestarting => LocalizationService.Instance["StageRestarting"];
        public string StageDone => LocalizationService.Instance["StageDone"];
        public string StageError => LocalizationService.Instance["StageError"];

        // Buttons
                        public string BtnUpdateOne => LocalizationService.Instance["BtnUpdateOne"];
                        public string BtnLaunch => LocalizationService.Instance["BtnLaunch"];
                        public string BtnEdit => LocalizationService.Instance["BtnEdit"];
                                public string BtnRollback => LocalizationService.Instance["BtnRollback"];
                                public string RollbackDialogTitle => LocalizationService.Instance["RollbackDialogTitle"];
                                public string RollbackDo => LocalizationService.Instance["RollbackDo"];
                                public string RollbackEmpty => LocalizationService.Instance["RollbackEmpty"];
                                public string RollbackHint => LocalizationService.Instance["RollbackHint"];
                                public string RollbackDone => LocalizationService.Instance["RollbackDone"];
                                public string AllowUnsignedInstaller => LocalizationService.Instance["AllowUnsignedInstaller"];

                        // Catalog Hero
                public string CatalogHeroTitle => LocalizationService.Instance["CatalogHeroTitle"];
                public string CatalogMetricTotal => LocalizationService.Instance["CatalogMetricTotal"];
                public string CatalogMetricTracked => LocalizationService.Instance["CatalogMetricTracked"];
                public string CatalogMetricUpdates => LocalizationService.Instance["CatalogMetricUpdates"];
                public string CatalogAddCustom => LocalizationService.Instance["CatalogAddCustom"];

                // Winget Hero
                                public string WingetHeroTitle => LocalizationService.Instance["WingetHeroTitle"];
                                public string WingetMetricTotal => LocalizationService.Instance["WingetMetricTotal"];
                                public string WingetMetricUpdates => LocalizationService.Instance["WingetMetricUpdates"];
                                public string WingetMetricPending => LocalizationService.Instance["WingetMetricPending"];
                                public string WingetUpdateSelected => LocalizationService.Instance["WingetUpdateSelected"];
                                public string WingetScan => LocalizationService.Instance["WingetScan"];
                                public string WingetNoUpdates => LocalizationService.Instance["WingetNoUpdates"];
                                public string WingetNotAvailable => LocalizationService.Instance["WingetNotAvailable"];

                // Not implemented
                public string NotImplemented => LocalizationService.Instance["notImplemented"];

                // New UI keys (P-UI)
                public string FilterAll => LocalizationService.Instance["FilterAll"];
                public string FilterUpdates => LocalizationService.Instance["FilterUpdates"];
                public string FilterErrors => LocalizationService.Instance["FilterErrors"];
                public string FilterDisabled => LocalizationService.Instance["FilterDisabled"];
                public string SortByName => LocalizationService.Instance["SortByName"];
                public string SortByStatus => LocalizationService.Instance["SortByStatus"];
                public string NextCheckAt => LocalizationService.Instance["NextCheckAt"];
                public string CategoryAll => LocalizationService.Instance["CategoryAll"];
                public string CategoryUtility => LocalizationService.Instance["CategoryUtility"];
                public string CategoryMedia => LocalizationService.Instance["CategoryMedia"];
                public string CategoryNetwork => LocalizationService.Instance["CategoryNetwork"];
                public string CategorySecurity => LocalizationService.Instance["CategorySecurity"];
                public string CategoryProductivity => LocalizationService.Instance["CategoryProductivity"];
                public string CategoryDevelopment => LocalizationService.Instance["CategoryDevelopment"];
                public string BtnOpenCatalog => LocalizationService.Instance["BtnOpenCatalog"];
                public string OnboardingTitle => LocalizationService.Instance["OnboardingTitle"];
                public string OnboardingText => LocalizationService.Instance["OnboardingText"];
                public string OnboardingOk => LocalizationService.Instance["OnboardingOk"];
                public string MenuEdit => LocalizationService.Instance["MenuEdit"];
                public string SettingsToastToggle => LocalizationService.Instance["SettingsToastToggle"];
                public string SettingsToastHint => LocalizationService.Instance["SettingsToastHint"];
            }
