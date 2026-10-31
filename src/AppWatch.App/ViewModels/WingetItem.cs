using AppWatch.Core.Domain;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AppWatch.ViewModels;

public partial class WingetItem : ObservableObject
{
    public WingetItem(WingetUpgradeRow row) => Row = row;

    public WingetUpgradeRow Row { get; }

    public string Name => Row.Name;

    public string Id => Row.Id;

    /// <summary>Installed version resolved from the system (registry / exe), null when unknown.</summary>
    public string? SystemVersion => Row.SystemVersion;

    public string CurrentVersion => SystemVersion ?? Row.Update.CurrentVersion;

    public string AvailableVersion => Row.Update.AvailableVersion;

    public string Versions => $"{CurrentVersion} → {AvailableVersion}";

    [ObservableProperty]
    public partial bool IsSelected { get; set; } = true;

    [ObservableProperty]
    public partial string Status { get; set; } = "";

    [ObservableProperty]
    public partial double ProgressValue { get; set; }

    [ObservableProperty]
    public partial bool IsUpdating { get; set; }

    [ObservableProperty]
    public partial bool IsIndeterminate { get; set; } = true;

    [ObservableProperty]
    public partial string ProgressText { get; set; } = "";

    /// <summary>Running clock "2:14" while the winget operation is in flight.</summary>
    [ObservableProperty]
    public partial string ElapsedText { get; set; } = "";
}
