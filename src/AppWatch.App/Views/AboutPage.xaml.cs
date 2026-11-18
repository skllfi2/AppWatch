using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Windows.ApplicationModel.DataTransfer;
using AppWatch.Localization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AppWatch.Views;

public sealed partial class AboutPage : Page
{
    public Loc Loc => Loc.Instance;

    public string AppVersion =>
        typeof(AboutPage).Assembly.GetName().Version?.ToString(3) ?? "0.2.2";

    public string FrameworkVersion =>
        $".NET {Environment.Version} ({RuntimeInformation.FrameworkDescription})";

    public string OsVersion =>
        $"{Environment.OSVersion.VersionString} ({(Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit")})";

    public string ProcessArchitecture =>
        RuntimeInformation.ProcessArchitecture.ToString().ToUpperInvariant();

    public AboutPage()
    {
        InitializeComponent();
    }

    private void CheckAppWatchUpdate_Click(object sender, RoutedEventArgs e)
    {
        OpenUrl("https://github.com/skllfi2/AppWatch/releases");
    }

    private void CopySystemInfo_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var info = $"AppWatch v{AppVersion}\n" +
                       $"OS: {OsVersion}\n" +
                       $"Arch: {ProcessArchitecture}\n" +
                       $"Runtime: {FrameworkVersion}";

            var package = new DataPackage();
            package.SetText(info);
            Clipboard.SetContent(package);

            CopiedTeachingTip.IsOpen = true;
        }
        catch { }
    }

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch { }
    }
}
