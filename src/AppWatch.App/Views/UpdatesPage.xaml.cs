using AppWatch.Localization;
using AppWatch.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace AppWatch.Views;

/// <summary>
/// Updates page: shows tracked apps with available updates.
/// </summary>
public sealed partial class UpdatesPage : Page
{
    public MainViewModel ViewModel { get; }
    public Loc Loc => Loc.Instance;

    public UpdatesPage()
    {
        ViewModel = App.Resolve<MainViewModel>();
        InitializeComponent();
        DataContext = ViewModel;
    }
}