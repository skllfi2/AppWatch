using AppWatch.Core.Domain;
using AppWatch.Localization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AppWatch.ViewModels;

/// <summary>Search result card for the "Install from winget" section (W2).</summary>
public partial class WingetSearchItem : ObservableObject
{
    public WingetSearchItem(WingetSearchResult result) => Result = result;

    public WingetSearchResult Result { get; }

    public string Name => Result.Name;
    public string Id => Result.Id;
    public string Version => Result.Version;
    public string Source => Result.Source;

    /// <summary>Monogram brush color per first letter (C13 pattern parity with other cards).</summary>
    public string Monogram => Name.Length > 0 ? Name[..1].ToUpperInvariant() : "?";

    [ObservableProperty]
    public partial bool IsInstalling { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; } = "";

    public bool CanInstall => !IsInstalling && !Installed;

    [ObservableProperty]
    public partial bool Installed { get; set; }

    partial void OnInstalledChanged(bool value) => OnPropertyChanged(nameof(CanInstall));
    partial void OnIsInstallingChanged(bool value) => OnPropertyChanged(nameof(CanInstall));

    /// <summary>Localized "Install" button label (resolved per-item to keep x:Bind inside DataTemplate valid).</summary>
    public string BtnInstall => LocalizationService.Instance["wingetInstall"];

    /// <summary>Commands proxied from the shared MainViewModel so x:Bind inside DataTemplate can reach them.</summary>
    public IRelayCommand InstallWingetCommand => global::AppWatch.App.Resolve<MainViewModel>().InstallWingetPackageCommand;
}
