using System.ComponentModel;
using System.Runtime.CompilerServices;
using AppWatch.Core.Domain.Models;
using AppWatch.Localization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AppWatch.ViewModels;

public sealed partial class AddEditAppDialogViewModel : ObservableObject
{
    private readonly TrackedApp _app;
    private readonly bool _isNew;

    public AddEditAppDialogViewModel(TrackedApp? app = null)
    {
        _app = app ?? new TrackedApp
        {
            Name = "",
            RepoOwner = "",
            RepoName = "",
            UpdateKind = UpdateKind.PortableZip,
            ExecutableName = "",
            ProcessName = "",
            InstallDirectory = "",
            LaunchAfterUpdate = true,
            IgnorePreReleases = true,
            AutoDetectVersion = true,
            Strategy = UpdateStrategy.Auto,
        };
        _isNew = app is null;

        // Initialize from app
        Name = _app.Name;
        RepoOwner = _app.RepoOwner;
        RepoName = _app.RepoName;
        SelectedUpdateKindIndex = (int)_app.UpdateKind;
        SelectedUpdateStrategyIndex = (int)_app.Strategy;
        ExecutableName = _app.ExecutableName ?? "";
        ProcessName = _app.ProcessName ?? "";
        InstallDirectory = _app.InstallDirectory ?? "";
        WingetPackageId = _app.WingetPackageId ?? "";
        LaunchAfterUpdate = _app.LaunchAfterUpdate;
        IgnorePreReleases = _app.IgnorePreReleases;
        AllowUnsignedInstaller = _app.AllowUnsignedInstaller;
        AutoDetectVersion = _app.AutoDetectVersion;
    }

    public bool IsNewApp => _isNew;

    public string DialogTitle => _isNew ? LocalizationService.Instance["AddAppTitle"] : LocalizationService.Instance["EditAppTitle"];
    public string PrimaryButtonText => _isNew ? LocalizationService.Instance["BtnAdd"] : LocalizationService.Instance["BtnSave"];

    public Loc Loc => Loc.Instance;

    [ObservableProperty]
    public partial string Name { get; set; } = "";

    [ObservableProperty]
    public partial string RepoOwner { get; set; } = "";

    [ObservableProperty]
    public partial string RepoName { get; set; } = "";

    [ObservableProperty]
    public partial int SelectedUpdateKindIndex { get; set; } = 0;

    [ObservableProperty]
    public partial int SelectedUpdateStrategyIndex { get; set; } = 0;

    [ObservableProperty]
    public partial string ExecutableName { get; set; } = "";

    [ObservableProperty]
    public partial string ProcessName { get; set; } = "";

    [ObservableProperty]
    public partial string InstallDirectory { get; set; } = "";

    [ObservableProperty]
    public partial string WingetPackageId { get; set; } = "";

    [ObservableProperty]
    public partial bool LaunchAfterUpdate { get; set; } = true;

    /// <summary>B7: per-app consent to run unsigned installers.</summary>
    [ObservableProperty]
    public partial bool AllowUnsignedInstaller { get; set; }

    [ObservableProperty]
    public partial bool IgnorePreReleases { get; set; } = true;

    [ObservableProperty]
    public partial bool AutoDetectVersion { get; set; } = true;

    public bool ShowUpdateStrategy => SelectedUpdateKindIndex != (int)UpdateKind.WingetPackage;

    public bool ShowExecutableName => SelectedUpdateKindIndex != (int)UpdateKind.WingetPackage;

    public bool ShowProcessName => SelectedUpdateKindIndex != (int)UpdateKind.WingetPackage;

    public bool ShowWingetPackageId => SelectedUpdateKindIndex == (int)UpdateKind.WingetPackage;

    partial void OnSelectedUpdateKindIndexChanged(int value)
    {
        OnPropertyChanged(nameof(ShowUpdateStrategy));
        OnPropertyChanged(nameof(ShowExecutableName));
        OnPropertyChanged(nameof(ShowProcessName));
        OnPropertyChanged(nameof(ShowWingetPackageId));
    }

    public TrackedApp ToTrackedApp()
    {
        _app.Name = Name.Trim();
        _app.RepoOwner = RepoOwner.Trim();
        _app.RepoName = RepoName.Trim();
        _app.UpdateKind = (UpdateKind)SelectedUpdateKindIndex;
        _app.Strategy = (UpdateStrategy)SelectedUpdateStrategyIndex;
        _app.ExecutableName = string.IsNullOrWhiteSpace(ExecutableName) ? null : ExecutableName.Trim();
        _app.ProcessName = string.IsNullOrWhiteSpace(ProcessName) ? null : ProcessName.Trim();
        _app.InstallDirectory = string.IsNullOrWhiteSpace(InstallDirectory) ? null : InstallDirectory.Trim();
        _app.WingetPackageId = string.IsNullOrWhiteSpace(WingetPackageId) ? null : WingetPackageId.Trim();
        _app.LaunchAfterUpdate = LaunchAfterUpdate;
        _app.IgnorePreReleases = IgnorePreReleases;
        _app.AllowUnsignedInstaller = AllowUnsignedInstaller;
        _app.AutoDetectVersion = AutoDetectVersion;
        return _app;
    }
}