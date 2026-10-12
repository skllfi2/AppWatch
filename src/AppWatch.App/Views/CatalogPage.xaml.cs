using AppWatch.Localization;
using AppWatch.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace AppWatch.Views;

public sealed partial class CatalogPage : Page
{
    public MainViewModel ViewModel { get; }

    public Loc Loc => Loc.Instance;

    public CatalogPage()
    {
        ViewModel = App.Resolve<MainViewModel>();
        InitializeComponent();
    }

    // ZuiV2 pattern: back to dashboard with slide animation
    private void Back_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) =>
        App.MainWindow!.GoBackWithAnimation();
}
