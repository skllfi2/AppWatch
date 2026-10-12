using AppWatch.Platform;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AppWatch.Views;

/// <summary>
/// The application window - ZuiV2 shell pattern: TitleBar + bare Frame.
/// All navigation lives inside AppsPage (dashboard hub); subpages call
/// NavigateTo / GoBackWithAnimation.
/// </summary>
public sealed partial class MainWindow : Window
{
    private readonly PageTransitionManager _transitionManager;

    public MainWindow()
    {
        InitializeComponent();
        _transitionManager = new PageTransitionManager(ContentFrame);

        // ZuiV2 pattern: extend content into the title bar area so Mica
        // covers the whole window and the system caption buttons overlay
        // our transparent TitleBar control instead of a separate band.
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        CenterWindowOnScreen();

        ContentFrame.Navigate(typeof(AppsPage));
    }

    // === Navigation (ZuiV2 MainWindow pattern) ===

    public void NavigateTo(Type pageType)
    {
        if (ContentFrame.CurrentSourcePageType == pageType) return;
        _transitionManager.NavigateTo(pageType);
    }

    public void GoBackWithAnimation()
    {
        _transitionManager.NavigateBack();
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
