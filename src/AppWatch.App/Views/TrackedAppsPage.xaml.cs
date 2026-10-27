using AppWatch.Localization;
using AppWatch.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace AppWatch.Views;

/// <summary>
/// Tracked apps page: only apps added by user via "Add App" (GitHub/winget).
/// Shows current/next version, source, check-update and remove buttons.
/// </summary>
public sealed partial class TrackedAppsPage : Page
{
    public MainViewModel ViewModel { get; }
    public Loc Loc => Loc.Instance;

    public TrackedAppsPage()
    {
        ViewModel = App.Resolve<MainViewModel>();
        InitializeComponent();
        DataContext = ViewModel;
    }
}