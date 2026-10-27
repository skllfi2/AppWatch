using AppWatch.Core.Domain.Models;
using AppWatch.Localization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Media;

namespace AppWatch.ViewModels;

public partial class TrackedAppItem : ObservableObject
{
    // 8-color palette for monograms; matches the IconGenerator palette style
    private static readonly uint[] MonogramPalette =
    [
        0xFF60CDFF, 0xFF4CAF50, 0xFF9C27B0, 0xFFFB5E5E,
        0xFFFFB900, 0xFF616161, 0xFF0078D4, 0xFF9E9E9E,
    ];

    public TrackedAppItem(TrackedApp app) => App = app;

    public TrackedApp App { get; }

    public string Name => App.Name;

    public string Version => App.CurrentVersion ?? "?";

    public string Monogram => string.IsNullOrEmpty(App.Name) ? "?" : App.Name[..1].ToUpperInvariant();

    /// <summary>True when the launch button makes sense (install dir + exe known).</summary>
    public bool HasLaunchInfo => App.InstallDirectory is not null && App.ExecutableName is not null;

    /// <summary>Layer-1: Launch is the primary action only when there is nothing to update.</summary>
    public bool CanLaunchPrimary => !UpdateAvailable && HasLaunchInfo;

    /// <summary>True when the stage shows a file-operation progress bar.</summary>
    public bool IsStageProgress => Stage is UpdateStage.Downloading or UpdateStage.Replacing;

    public string StageText => Stage switch
    {
        UpdateStage.Checking => LocalizationService.Instance["StageChecking"],
        UpdateStage.Downloading => LocalizationService.Instance["StageDownloading"],
        UpdateStage.Replacing => LocalizationService.Instance["StageReplacing"],
        UpdateStage.Restarting => LocalizationService.Instance["StageRestarting"],
        UpdateStage.Done => LocalizationService.Instance["StageDone"],
        UpdateStage.Error => LocalizationService.Instance["StageError"],
        _ => "",
    };

    /// <summary>
    /// Status dot color; uses the DesignSystem semantic palette (theme-independent).
    /// </summary>
    public Brush StatusBrush => Stage switch
    {
        UpdateStage.Done => MakeBrush(0x2E, 0xA0, 0x43),
        UpdateStage.Error => MakeBrush(0xD8, 0x3B, 0x01),
        UpdateStage.Replacing => MakeBrush(0xFF, 0xB9, 0x00),
        UpdateStage.Checking or UpdateStage.Downloading or UpdateStage.Restarting => MakeBrush(0x00, 0x78, 0xD4),
        _ => MakeBrush(0x61, 0x61, 0x61),
    };

    public Brush MonogramBrush
    {
        get
        {
            var hash = Math.Abs(string.GetHashCode(App.Name, StringComparison.OrdinalIgnoreCase));
            var hex = MonogramPalette[hash % MonogramPalette.Length];
            return MakeBrush((byte)(hex >> 16), (byte)(hex >> 8), (byte)hex);
        }
    }

    /// <summary>GitHub repo avatar URL for card icon (from TrackedApp.AvatarUrl).</summary>
        public string? AvatarUrl => App.AvatarUrl;

        [ObservableProperty]
        public partial UpdateStage Stage { get; set; } = UpdateStage.Idle;

    [ObservableProperty]
    public partial string StatusText { get; set; } = "";

    /// <summary>D5: engine Detail for the last check/apply (error reason etc.) — shown in the card tooltip.</summary>
    [ObservableProperty]
    public partial string? StatusDetail { get; set; }

    /// <summary>D5: tooltip = stage caption + engine detail (why the error happened).</summary>
    public string StatusTooltip =>
        string.IsNullOrWhiteSpace(StatusDetail) ? StageText : $"{StageText}\n{StatusDetail}";

    [ObservableProperty]
    public partial double ProgressValue { get; set; }

    [ObservableProperty]
    public partial bool UpdateAvailable { get; set; }

    /// <summary>A1: newest release tag from the last check (drives "skip this version").</summary>
    [ObservableProperty]
    public partial string? LatestTag { get; set; }

    /// <summary>Layer-1 UI: release notes of the pending update (markdown body, shown in the details dialog).</summary>
    [ObservableProperty]
    public partial string? PendingNotes { get; set; }

    /// <summary>Layer-1 UI: the pending version, or null when up to date.</summary>
    public string? AvailableVersion => UpdateAvailable ? LatestTag : null;

    /// <summary>A1: true when the user pinned the current version (engine returns Pinned).</summary>
    public bool IsPinned => App.PinnedVersion is not null;

    /// <summary>A1: true when the user skipped a specific version.</summary>
    public bool HasSkipRule => App.SkippedVersion is not null;

    /// <summary>A1: menu captions resolved per-item for x:Bind inside DataTemplate.</summary>
    public string MenuSkipVersion => LocalizationService.Instance["MenuSkipVersion"];
    public string MenuUpdateRules => LocalizationService.Instance["MenuUpdateRules"];

    /// <summary>Localized relative "last checked" caption (P-UI informative-lite).</summary>
    public string LastCheckText
    {
        get
        {
            var loc = LocalizationService.Instance;
            if (App.LastCheckUtc is not { } t)
                return loc["NeverChecked"];
            var delta = DateTimeOffset.UtcNow - t;
            if (delta < TimeSpan.FromMinutes(1))
                return loc["CheckedJustNow"];
            if (delta < TimeSpan.FromHours(1))
                return string.Format(loc["CheckedMinutesAgo"], (int)delta.TotalMinutes);
            if (delta < TimeSpan.FromDays(1))
                return string.Format(loc["CheckedHoursAgo"], (int)delta.TotalHours);
            return string.Format(loc["CheckedDaysAgo"], (int)delta.TotalDays);
        }
    }
    public string MenuPinCurrent => IsPinned
        ? LocalizationService.Instance["MenuUnpin"]
        : LocalizationService.Instance["MenuPinCurrent"];
    public string MenuResetRules => LocalizationService.Instance["MenuResetRules"];
    public string RuleBadge => IsPinned
        ? LocalizationService.Instance["BadgePinned"]
        : HasSkipRule ? LocalizationService.Instance["BadgeSkipped"] : "";

    partial void OnStageChanged(UpdateStage value)
    {
        OnPropertyChanged(nameof(StatusBrush));
        OnPropertyChanged(nameof(StageText));
        OnPropertyChanged(nameof(IsStageProgress));
        // Stage transitions bracket every check/apply — by the time the stage
        // leaves Checking, the engine has already persisted App.LastCheckUtc.
        OnPropertyChanged(nameof(LastCheckText));
    }

    partial void OnStatusDetailChanged(string? value) => OnPropertyChanged(nameof(StatusTooltip));

    public void ReportVersionChanged() => OnPropertyChanged(nameof(Version));

    public void Refresh()
    {
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Version));
        OnPropertyChanged(nameof(Monogram));
        OnPropertyChanged(nameof(MonogramBrush));
        OnPropertyChanged(nameof(HasLaunchInfo));
        OnPropertyChanged(nameof(IsStageProgress));
        OnPropertyChanged(nameof(StageText));
        OnPropertyChanged(nameof(StatusBrush));
        OnPropertyChanged(nameof(BtnUpdateOne));
        OnPropertyChanged(nameof(BtnLaunch));
        OnPropertyChanged(nameof(RollbackAvailable));
        OnPropertyChanged(nameof(IsPinned));
        OnPropertyChanged(nameof(HasSkipRule));
        OnPropertyChanged(nameof(MenuPinCurrent));
        OnPropertyChanged(nameof(RuleBadge));
        OnPropertyChanged(nameof(StatusTooltip));
        OnPropertyChanged(nameof(LastCheckText));
    }

    /// <summary>Localized "Update" button label (resolved per-item to keep x:Bind inside DataTemplate valid).</summary>
    public string BtnUpdateOne => LocalizationService.Instance["BtnUpdateOne"];

    /// <summary>Localized "Launch" button label.</summary>
    public string BtnLaunch => LocalizationService.Instance["BtnLaunch"];

    /// <summary>Commands proxied from the shared MainViewModel so x:Bind inside DataTemplate can reach them.</summary>
    public IRelayCommand UpdateOneCommand => global::AppWatch.App.Resolve<MainViewModel>().UpdateOneCommand;

    public IRelayCommand LaunchAppCommand => global::AppWatch.App.Resolve<MainViewModel>().LaunchAppCommand;

    public IRelayCommand EditAppCommand => global::AppWatch.App.Resolve<MainViewModel>().EditAppCommand;

    /// <summary>Localized "Edit" button label.</summary>
    public string BtnEdit => LocalizationService.Instance["BtnEdit"];

    /// <summary>Localized "Rollback" button label (B10).</summary>
    public string BtnRollback => LocalizationService.Instance["BtnRollback"];

    /// <summary>Localized "Edit" menu item label (P-UI).</summary>
    public string MenuEdit => LocalizationService.Instance["MenuEdit"];

    /// <summary>Human-readable source display: GitHub / winget / Installer / External.</summary>
    public string SourceDisplay => App.UpdateKind switch
    {
        UpdateKind.WingetPackage => LocalizationService.Instance["SourceWinget"],
        UpdateKind.Installer => LocalizationService.Instance["SourceInstaller"],
        UpdateKind.External => LocalizationService.Instance["SourceExternal"],
        _ => LocalizationService.Instance["SourceGitHub"], // PortableZip, PortableExe
    };

    /// <summary>B10: true when the version cache holds at least one older snapshot for this app.</summary>
    public bool RollbackAvailable =>
        App.InstallDirectory is not null
        && global::AppWatch.App.Resolve<Infrastructure.System.VersionStore>().Any(App.Name);

    public IRelayCommand RollbackAppCommand => global::AppWatch.App.Resolve<MainViewModel>().RollbackAppCommand;

    /// <summary>A1: per-app update rules commands (proxied from MainViewModel for DataTemplate x:Bind).</summary>
    public IRelayCommand SkipVersionCommand => global::AppWatch.App.Resolve<MainViewModel>().SkipVersionCommand;
    public IRelayCommand PinCurrentCommand => global::AppWatch.App.Resolve<MainViewModel>().PinCurrentCommand;
    public IRelayCommand ResetRulesCommand => global::AppWatch.App.Resolve<MainViewModel>().ResetRulesCommand;

    /// <summary>Layer-1 UI: opens the app details dialog (versions, release notes, history, automation).</summary>
    public IRelayCommand ShowDetailsCommand => global::AppWatch.App.Resolve<MainViewModel>().ShowAppDetailsCommand;

    /// <summary>Uninstall/untrack this app (with confirm) — proxied from MainViewModel.</summary>
    public IRelayCommand UninstallAppCommand => global::AppWatch.App.Resolve<MainViewModel>().UninstallAppCommand;

    /// <summary>Menu caption for uninstall.</summary>
    public string MenuUninstall => LocalizationService.Instance["MenuUninstall"];

    private static Brush MakeBrush(byte r, byte g, byte b) =>
        new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, r, g, b));
}
