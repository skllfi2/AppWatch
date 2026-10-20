using AppWatch.Localization;
using AppWatch.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AppWatch.Views;

public sealed partial class WingetPage : Page
{
    public MainViewModel ViewModel { get; }

    public Loc Loc => Loc.Instance;

    public WingetPage()
    {
        ViewModel = App.Resolve<MainViewModel>();
        InitializeComponent();
    }

    public bool IsWingetUnavailable => !ViewModel.WingetAvailable;

    public Visibility IsWingetAvailable => ViewModel.WingetAvailable ? Visibility.Visible : Visibility.Collapsed;

    public string WingetUpdateSelectedText => Loc.Instance["wingetUpdateSelected"];
}
