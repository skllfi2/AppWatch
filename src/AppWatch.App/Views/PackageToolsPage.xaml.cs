using AppWatch.Localization;
using AppWatch.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace AppWatch.Views;

public sealed partial class PackageToolsPage : Page
{
    public PackageToolsViewModel ViewModel { get; }
    public Loc Loc => Loc.Instance;

    public PackageToolsPage()
    {
        ViewModel = App.Resolve<PackageToolsViewModel>();
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (ViewModel.AllTools.Count == 0 && !ViewModel.IsScanning)
        {
            _ = ViewModel.ScanCommand.ExecuteAsync(null);
        }
    }
}
