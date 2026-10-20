using AppWatch.Localization;
using AppWatch.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace AppWatch.Views;

/// <summary>
/// Home page (ZuiV2 DashboardPage pattern): hero banner with metrics and the
/// tracked-apps list. Navigation lives in the MainWindow NavigationView pane.
/// </summary>
public sealed partial class AppsPage : Page
{
    public MainViewModel ViewModel { get; }

    public Loc Loc => Loc.Instance;

    public AppsPage()
    {
        ViewModel = App.Resolve<MainViewModel>();
        InitializeComponent();
    }
}
