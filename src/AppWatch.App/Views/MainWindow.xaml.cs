using System.Linq;
using AppWatch.Localization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AppWatch.Views;

/// <summary>
/// ZuiV2 shell v2: TitleBar + NavigationView + Frame.
/// The left pane is the single source of navigation; subpages no longer
/// carry back buttons — the pane is always visible.
/// </summary>
public sealed partial class MainWindow : Window
{
    public Loc Loc => Loc.Instance;

    public MainWindow()
    {
        InitializeComponent();

        // ZuiV2 pattern: extend content into the title bar area so Mica
        // covers the whole window and the system caption buttons overlay
        // our transparent TitleBar control instead of a separate band.
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        CenterWindowOnScreen();

        // Initial selection drives the first navigation via SelectionChanged.
        Nav.SelectedItem = NavItemHome;
    }

    // === Navigation (NavigationView-driven) ===

    private static readonly System.Collections.Generic.Dictionary<string, Type> PageByTag = new()
    {
        ["home"] = typeof(AppsPage),
        ["catalog"] = typeof(CatalogPage),
        ["winget"] = typeof(WingetPage),
        ["history"] = typeof(HistoryPage),
        ["settings"] = typeof(SettingsPage),
        ["about"] = typeof(AboutPage),
    };

    private void Nav_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is not NavigationViewItem item || item.Tag is not string tag)
            return;
        if (!PageByTag.TryGetValue(tag, out var page))
            return;
        if (ContentFrame.CurrentSourcePageType != page)
            ContentFrame.Navigate(page);
    }

    /// <summary>
    /// Programmatic navigation that keeps the pane selection in sync.
    /// Selecting an item raises SelectionChanged, which performs the actual Navigate.
    /// </summary>
    public void NavigateTo(Type pageType)
    {
        var tag = PageByTag.FirstOrDefault(kv => kv.Value == pageType).Key;
        if (tag is not null)
        {
            var item = Nav.MenuItems.OfType<NavigationViewItem>()
                .Concat(Nav.FooterMenuItems.OfType<NavigationViewItem>())
                .FirstOrDefault(i => (string?)i.Tag == tag);
            if (item is not null)
            {
                Nav.SelectedItem = item;
                return;
            }
        }
        ContentFrame.Navigate(pageType);
    }

    /// <summary>Centers the window on the primary work area (ZuiV2 pattern).</summary>
    private void CenterWindowOnScreen()
    {
        try
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var monitor = MonitorFromWindow(hwnd, 2 /* MONITOR_DEFAULTTONEAREST */);
            var monitorInfo = new MONITORINFO { cbSize = 40 };
            if (GetMonitorInfo(monitor, ref monitorInfo))
            {
                var workWidth = monitorInfo.rcWork.Right - monitorInfo.rcWork.Left;
                var workHeight = monitorInfo.rcWork.Bottom - monitorInfo.rcWork.Top;
                var windowWidth = 1200;
                var windowHeight = 800;
                var x = monitorInfo.rcWork.Left + (workWidth - windowWidth) / 2;
                var y = monitorInfo.rcWork.Top + (workHeight - windowHeight) / 2;
                AppWindow.Move(new Windows.Graphics.PointInt32(x, y));
                AppWindow.Resize(new Windows.Graphics.SizeInt32(windowWidth, windowHeight));
            }
        }
        catch { }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }
}
