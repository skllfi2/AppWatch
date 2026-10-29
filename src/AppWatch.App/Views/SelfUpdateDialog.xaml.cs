using AppWatch.Localization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;

namespace AppWatch.Views;

/// <summary>
/// Self-update dialog: shows changelog, download progress, and actions.
/// Created in code-behind to avoid XAML compiler locale issues.
/// </summary>
public sealed partial class SelfUpdateDialog : ContentDialog
{
    public Loc Loc => Loc.Instance;

    public SelfUpdateViewModel ViewModel { get; }

    public SelfUpdateDialog(string version, string changelog, bool isZipFlow)
    {
        ViewModel = new SelfUpdateViewModel(version, changelog, isZipFlow);
        DataContext = ViewModel;
        InitializeComponent();
        XamlRoot = App.MainWindow?.Content.XamlRoot;
    }

    public static async Task<SelfUpdateResult> ShowAsync(string version, string changelog, bool isZipFlow, IProgress<double>? downloadProgress = null)
    {
        var dialog = new SelfUpdateDialog(version, changelog, isZipFlow);
        if (downloadProgress != null)
            dialog.ViewModel.SetDownloadProgress(downloadProgress);

        var result = await dialog.ShowAsync();
        return result switch
        {
            ContentDialogResult.Primary => SelfUpdateResult.UpdateNow,
            ContentDialogResult.Secondary => SelfUpdateResult.SkipVersion,
            _ => SelfUpdateResult.RemindLater,
        };
    }
}

public enum SelfUpdateResult
{
    UpdateNow,
    SkipVersion,
    RemindLater,
    Cancelled,
}

public partial class SelfUpdateViewModel : ObservableObject
{
    private readonly bool _isZipFlow;
    private IProgress<double>? _downloadProgress;

    public SelfUpdateViewModel(string version, string changelog, bool isZipFlow)
    {
        Version = version;
        Changelog = changelog;
        _isZipFlow = isZipFlow;
        IsDownloading = false;
        DownloadProgress = 0;
        DownloadSpeed = "";
    }

    public string Version { get; }
    public string Changelog { get; }
    public string Title => $"{Loc.Instance["SelfUpdateDialogTitle"]} {Version}";

    [ObservableProperty]
    public partial bool IsDownloading { get; set; }

    [ObservableProperty]
    public partial double DownloadProgress { get; set; }

    [ObservableProperty]
    public partial string DownloadSpeed { get; set; } = "";

    [ObservableProperty]
    public partial bool IsProgressIndeterminate { get; set; } = true;

    [ObservableProperty]
    public partial string StatusText { get; set; } = "";

    public void SetDownloadProgress(IProgress<double> progress)
    {
        _downloadProgress = new Progress<double>(p =>
        {
            DownloadProgress = p;
            // Speed will be updated by the caller via StatusText or separate property
        });
    }

    public void ReportProgress(double percent, string speed)
    {
        DownloadProgress = percent;
        DownloadSpeed = speed;
        IsProgressIndeterminate = false;
        StatusText = $"{Loc.Instance["SelfUpdateDownloading"]} {(int)(percent * 100)}% · {speed}";
    }

    public void ReportPhase(string phase)
    {
        StatusText = phase;
    }

    [RelayCommand]
    private void UpdateNow() { }

    [RelayCommand]
    private void SkipVersion() { }

    [RelayCommand]
    private void RemindLater() { }
}