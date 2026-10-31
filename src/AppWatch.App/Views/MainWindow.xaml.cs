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

            var iconPath = System.IO.Path.Combine(System.AppContext.BaseDirectory, "Assets", "AppIcon.ico");
            if (System.IO.File.Exists(iconPath))
            {
                AppWindow.SetIcon(iconPath);
            }

            var logoPath = System.IO.Path.Combine(System.AppContext.BaseDirectory, "Assets", "Square44x44Logo.scale-200.png");
            if (System.IO.File.Exists(logoPath))
            {
                AppTitleBar.IconSource = new Microsoft.UI.Xaml.Controls.BitmapIconSource
                {
                    UriSource = new Uri(logoPath),
                    ShowAsMonochrome = false
                };
            }

            CenterWindowOnScreen();

            // Initial selection drives the first navigation via SelectionChanged.
                        Nav.SelectedItem = NavItemTrackedApps;

            WireNavBadges();
        }

    // === Nav badges: update counters on Updates and Winget ===
        // NavigationViewItem.InfoBadge crashes the XAML compiler (WMC9999) when
        // declared in XAML, but works fine instantiated from code-behind.

        private void WireNavBadges()
        {
            var vm = global::AppWatch.App.Resolve<ViewModels.MainViewModel>();

            var updatesBadge = NewBadge();
            NavItemUpdates.InfoBadge = updatesBadge;

            var wingetItem = Nav.MenuItems.OfType<NavigationViewItem>()
                .FirstOrDefault(i => (string?)i.Tag == "winget");
            InfoBadge? wingetBadge = null;
            if (wingetItem is not null)
            {
                wingetBadge = NewBadge();
                wingetItem.InfoBadge = wingetBadge;
            }

            void UpdateVisuals()
            {
                HeaderProgressBar.Visibility = vm.IsAnyChecking ? Visibility.Visible : Visibility.Collapsed;

                if (vm.IsCheckingApps)
                {
                    updatesBadge.IconSource = new FontIconSource { Glyph = "\uE895" };
                    updatesBadge.Visibility = Visibility.Visible;
                }
                else
                {
                    updatesBadge.IconSource = null;
                    SyncBadge(updatesBadge, vm.UpdatesAvailableCount);
                }

                if (wingetBadge is not null)
                {
                    if (vm.IsScanningWinget)
                    {
                        wingetBadge.IconSource = new FontIconSource { Glyph = "\uE895" };
                        wingetBadge.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        wingetBadge.IconSource = null;
                        SyncBadge(wingetBadge, vm.WingetUpdatesAvailable);
                    }
                }
            }

            vm.PropertyChanged += (_, e) =>
            {
                switch (e.PropertyName)
                {
                    case nameof(ViewModels.MainViewModel.UpdatesAvailableCount):
                    case nameof(ViewModels.MainViewModel.WingetUpdatesAvailable):
                    case nameof(ViewModels.MainViewModel.IsCheckingApps):
                    case nameof(ViewModels.MainViewModel.IsScanningWinget):
                    case nameof(ViewModels.MainViewModel.IsAnyChecking):
                        DispatcherQueue.TryEnqueue(UpdateVisuals);
                        break;
                }
            };

            UpdateVisuals();
        }

    private static InfoBadge NewBadge() => new()
    {
        FontSize = 11,
        Visibility = Visibility.Collapsed,
    };

    private static void SyncBadge(InfoBadge badge, int count)
    {
        badge.Value = count;
        badge.Visibility = count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    // === Navigation (NavigationView-driven) ===

    private static readonly System.Collections.Generic.Dictionary<string, Type> PageByTag = new()
            {
                ["trackedapps"] = typeof(Views.TrackedAppsPage),
                ["updates"] = typeof(Views.UpdatesPage),
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
        {
            ContentFrame.Navigate(page, null, new Microsoft.UI.Xaml.Media.Animation.EntranceNavigationTransitionInfo());
            App.Resolve<Platform.SoundService>().PlayNavigation();
        }
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
        ContentFrame.Navigate(pageType, null, new Microsoft.UI.Xaml.Media.Animation.EntranceNavigationTransitionInfo());
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

    // === Drag & Drop support (Phase 3) ===
    private void Grid_DragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.StorageItems) ||
            e.DataView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.Text) ||
            e.DataView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.Uri))
        {
            e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
            e.DragUIOverride.Caption = Loc.DropToAddApp;
            e.DragUIOverride.IsCaptionVisible = true;
            e.DragUIOverride.IsGlyphVisible = true;
        }
        else
        {
            e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.None;
        }
    }

    private async void Grid_Drop(object sender, DragEventArgs e)
    {
        var vm = global::AppWatch.App.Resolve<ViewModels.MainViewModel>();
        if (e.DataView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.StorageItems))
        {
            var items = await e.DataView.GetStorageItemsAsync();
            if (items.Count == 1 && items[0] is Windows.Storage.IStorageFile file)
            {
                await vm.HandleDroppedFileAsync(file.Path);
            }
            else if (items.Count > 1)
            {
                var paths = items.OfType<Windows.Storage.IStorageFile>().Select(f => f.Path).ToList();
                var text = string.Join(Environment.NewLine, paths);
                await vm.HandleDroppedInputAsync(text);
            }
        }
        else if (e.DataView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.Uri))
        {
            var uri = await e.DataView.GetUriAsync();
            if (uri is not null)
            {
                await vm.HandleDroppedInputAsync(uri.ToString());
            }
        }
        else if (e.DataView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.Text))
        {
            var text = await e.DataView.GetTextAsync();
            if (!string.IsNullOrWhiteSpace(text))
            {
                await vm.HandleDroppedInputAsync(text);
            }
        }
    }
}
