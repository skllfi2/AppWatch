using AppWatch.Localization;
using AppWatch.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace AppWatch.Views;

public sealed partial class SettingsPage : Page
{
    public SettingsViewModel ViewModel { get; }

    public Loc Loc => Loc.Instance;

    public SettingsPage()
    {
        ViewModel = App.Resolve<SettingsViewModel>();
        InitializeComponent();
    }

    // ZuiV2 pattern: back to dashboard with slide animation
    private void Back_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) =>
        App.MainWindow!.GoBackWithAnimation();
}
