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

    [ObservableProperty]
    public partial UpdateStage Stage { get; set; } = UpdateStage.Idle;

    [ObservableProperty]
    public partial string StatusText { get; set; } = "";

    [ObservableProperty]
    public partial double ProgressValue { get; set; }

    [ObservableProperty]
    public partial bool UpdateAvailable { get; set; }

    partial void OnStageChanged(UpdateStage value)
    {
        OnPropertyChanged(nameof(StatusBrush));
        OnPropertyChanged(nameof(StageText));
        OnPropertyChanged(nameof(IsStageProgress));
    }

    public void ReportVersionChanged() => OnPropertyChanged(nameof(Version));

    /// <summary>Localized "Update" button label (resolved per-item to keep x:Bind inside DataTemplate valid).</summary>
    public string BtnUpdateOne => LocalizationService.Instance["BtnUpdateOne"];

    /// <summary>Localized "Launch" button label.</summary>
    public string BtnLaunch => LocalizationService.Instance["BtnLaunch"];

    /// <summary>Commands proxied from the shared MainViewModel so x:Bind inside DataTemplate can reach them.</summary>
    public IRelayCommand UpdateOneCommand => global::AppWatch.App.Resolve<MainViewModel>().UpdateOneCommand;

    public IRelayCommand LaunchAppCommand => global::AppWatch.App.Resolve<MainViewModel>().LaunchAppCommand;

    private static Brush MakeBrush(byte r, byte g, byte b) =>
        new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, r, g, b));
}
