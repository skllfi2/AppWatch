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
}
