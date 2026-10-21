using AppWatch.Localization;
using AppWatch.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AppWatch.Views;

/// <summary>
/// Combined Home + Catalog page with TabView.
/// Tab 0 = Home (tracked apps with groups), Tab 1 = Catalog (search/install).
/// </summary>
public sealed partial class HomeCatalogPage : Page
{
    public MainViewModel ViewModel { get; }

    public Loc Loc => Loc.Instance;

    public HomeCatalogPage()
    {
        ViewModel = App.Resolve<MainViewModel>();
        InitializeComponent();
    }
}