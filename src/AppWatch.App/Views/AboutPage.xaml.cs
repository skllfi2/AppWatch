using AppWatch.Localization;
using Microsoft.UI.Xaml.Controls;

namespace AppWatch.Views;

public sealed partial class AboutPage : Page
{
    public Loc Loc => Loc.Instance;

    public string AppVersion =>
        typeof(AboutPage).Assembly.GetName().Version?.ToString(3) ?? "0.1.0";

    public AboutPage()
    {
        InitializeComponent();
    }
}
