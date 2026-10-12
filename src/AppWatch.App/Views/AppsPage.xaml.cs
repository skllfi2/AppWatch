using AppWatch.Localization;
using AppWatch.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AppWatch.Views;

/// <summary>
/// Dashboard hub (ZuiV2 DashboardPage pattern): hero banner with metrics,
/// tracked-apps list, quick navigation cards to subpages.
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

    // === Navigation (ZuiV2 NavCard_Click pattern) ===

    private void NavCard_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string tag })
        {
            var pageType = tag switch
            {
                "catalog" => typeof(CatalogPage),
                "winget" => typeof(WingetPage),
                "settings" => typeof(SettingsPage),
                "about" => typeof(AboutPage),
                _ => null,
            };
            if (pageType is not null)
                App.MainWindow!.NavigateTo(pageType);
        }
    }
}
