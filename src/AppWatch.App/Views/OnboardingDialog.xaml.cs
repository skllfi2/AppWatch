using AppWatch.Localization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AppWatch.Views;

public sealed partial class OnboardingDialog : ContentDialog
{
    public Loc Loc => Loc.Instance;

    public OnboardingDialog()
    {
        InitializeComponent();
        XamlRoot = App.MainWindow?.Content.XamlRoot;
    }

    public static async Task ShowIfNeededAsync()
    {
        var config = App.Resolve<AppWatch.Core.Abstractions.IConfigStore>().Load();
        if (!config.ShowOnboarding) return;

        var dialog = new OnboardingDialog();
        await dialog.ShowAsync();

        // Mark as shown
        config.ShowOnboarding = false;
        App.Resolve<AppWatch.Core.Abstractions.IConfigStore>().Save(config);
    }
}