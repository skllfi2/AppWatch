using AppWatch.Localization;
using AppWatch.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace AppWatch.Views;

public sealed partial class SettingsPage : Page
{
    public SettingsViewModel ViewModel { get; }

    public Loc Loc => Loc.Instance;

    public SettingsPage()
    {
        ViewModel = App.Resolve<SettingsViewModel>();
        InitializeComponent();
        // C12: page is cached (NavigationCacheMode=Required) — refresh on construction too
        ViewModel.RefreshScheduleInfo();
    }

    // C12: fires on every navigation TO the page, including cached instances —
    // keeps the next-run times fresh after background monitors update config.
    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ViewModel.RefreshScheduleInfo();
    }

    // ZuiV2 pattern: back to dashboard with slide animation
    private void Back_Click(object sender, RoutedEventArgs e) =>
        App.MainWindow!.GoBackWithAnimation();
}
