// Ported from ZuiV2 Platform/TrayService.cs (our own code).
// WinUI 3 has no native tray support and AppWindow.Hide()/Show() is unreliable —
// the proven approach: Shell_NotifyIconW for the icon, ShowWindow(hwnd) for hide/show,
// a hidden message window with a WndProc for tray callbacks.
using System.Runtime.InteropServices;
using AppWatch.Localization;

namespace AppWatch.Platform;

/// <summary>
/// System tray icon service using Shell_NotifyIconW P/Invoke, with a dark-mode
/// context menu and balloon notifications.
/// </summary>
public sealed class TrayService : IDisposable
{
    private const int NIM_ADD = 0x00000000;
    private const int NIM_MODIFY = 0x00000001;
    private const int NIM_DELETE = 0x00000002;

    private const int NIF_MESSAGE = 0x00000001;
    private const int NIF_ICON = 0x00000002;
    private const int NIF_TIP = 0x00000004;
    private const int NIF_INFO = 0x00000010;

    private const int WM_LBUTTONDOWN = 0x0201;
    private const int WM_LBUTTONDBLCLK = 0x0203;
    private const int WM_RBUTTONUP = 0x0205;

    private const int SW_HIDE = 0;
    private const int SW_RESTORE = 9;

    private const int TPM_RIGHTBUTTON = 0x0002;
    private const int TPM_RETURNCMD = 0x0100;

    private const uint MF_STRING = 0x00000000;
    private const uint MF_SEPARATOR = 0x00000800;
    private const uint MF_CHECKED = 0x00000008;

    private const int ID_SHOW = 1000;
    private const int ID_CHECK_UPDATES = 1001;
    private const int ID_AUTOSTART = 1002;
    private const int ID_EXIT = 1003;

    private const uint WM_APP = 0x8000;

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATAW
    {
        public int cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        public int dwState;
        public int dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        public uint uTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        public uint dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WNDCLASSW
    {
        public uint style; public IntPtr lpfnWndProc; public int cbClsExtra; public int cbWndExtra;
        public IntPtr hInstance; public IntPtr hIcon; public IntPtr hCursor; public IntPtr hbrBackground;
        public string? lpszMenuName; public string? lpszClassName;
    }

    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    private static WndProcDelegate? _wndProcDelegate;
    private static TrayService? _instance;

    private NOTIFYICONDATAW _nid;
    private readonly object _lock = new();
    private bool _disposed;
    private bool _isVisible;
    private IntPtr _messageWindow;

    public event Action? TrayIconClicked;
    public event Action? ShowRequested;
    public event Action? ExitRequested;
    public event Action? CheckUpdatesRequested;
    public event Action? AutostartToggleRequested;

    private Func<bool>? _isAutostartEnabled;

    public void Create(IntPtr hwnd, IntPtr iconHandle, string tooltip, Func<bool>? isAutostartEnabled = null)
    {
        _instance = this;
        _isAutostartEnabled = isAutostartEnabled;

        EnableDarkModeForMenus();

        _wndProcDelegate = MessageWndProc;
        var wc = new WNDCLASSW
        {
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProcDelegate),
            lpszClassName = "AppWatch_TrayMsg",
            hInstance = GetModuleHandle(null),
        };
        RegisterClassW(ref wc);

        _messageWindow = CreateWindowEx(
            0, wc.lpszClassName ?? "", "", 0,
            0, 0, 0, 0,
            new IntPtr(-3) /* HWND_MESSAGE */, IntPtr.Zero, wc.hInstance, IntPtr.Zero);

        ApplyDarkModeToWindow();

        lock (_lock)
        {
            _nid = new NOTIFYICONDATAW
            {
                cbSize = Marshal.SizeOf<NOTIFYICONDATAW>(),
                hWnd = _messageWindow,
                uID = 1,
                uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
                uCallbackMessage = WM_APP,
                hIcon = iconHandle,
                szTip = tooltip,
            };

            Shell_NotifyIconW(NIM_ADD, ref _nid);
            _isVisible = true;
        }
    }

    public void UpdateIcon(IntPtr newIconHandle)
    {
        lock (_lock)
        {
            if (!_isVisible) return;
            _nid.hIcon = newIconHandle;
            _nid.uFlags = NIF_ICON;
            Shell_NotifyIconW(NIM_MODIFY, ref _nid);
            _nid.uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP;
        }
    }

    public void UpdateTooltip(string tooltip)
    {
        lock (_lock)
        {
            if (!_isVisible) return;
            _nid.szTip = tooltip;
            _nid.uFlags = NIF_TIP;
            Shell_NotifyIconW(NIM_MODIFY, ref _nid);
            _nid.uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP;
        }
    }

    public void ShowBalloon(string title, string message, int timeoutMs = 3000)
    {
        lock (_lock)
        {
            if (!_isVisible) return;
            _nid.uFlags |= NIF_INFO;
            _nid.szInfoTitle = title;
            _nid.szInfo = message;
            _nid.uTimeoutOrVersion = (uint)(timeoutMs / 1000);
            _nid.dwInfoFlags = 1;
            Shell_NotifyIconW(NIM_MODIFY, ref _nid);
            _nid.uFlags &= unchecked((uint)~NIF_INFO);
        }
    }

    public void HideWindow(IntPtr hwnd) => ShowWindow(hwnd, SW_HIDE);

    public void ShowWindowRestore(IntPtr hwnd)
    {
        ShowWindow(hwnd, SW_RESTORE);
        SetForegroundWindow(hwnd);
    }

    public void Remove()
    {
        lock (_lock)
        {
            if (_isVisible) { Shell_NotifyIconW(NIM_DELETE, ref _nid); _isVisible = false; }
        }
    }

    /// <summary>Destroys the hidden message window and removes the tray icon. Call before process exit.</summary>
    public void Destroy()
    {
        Remove();
        if (_messageWindow != IntPtr.Zero)
        {
            DestroyWindow(_messageWindow);
            _messageWindow = IntPtr.Zero;
        }
    }

    public void Dispose()
    {
        if (!_disposed) { Remove(); _disposed = true; }
    }

    private static IntPtr MessageWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == WM_APP) { _instance?.HandleTrayCallback(lParam); return IntPtr.Zero; }
        return DefWindowProc(hWnd, msg, wParam, lParam);
    }

    private void HandleTrayCallback(IntPtr lParam)
    {
        var mouseMsg = lParam.ToInt32() & 0xFFFF;
        switch (mouseMsg)
        {
            case WM_LBUTTONDOWN:
            case WM_LBUTTONDBLCLK:
                TrayIconClicked?.Invoke();
                break;
            case WM_RBUTTONUP:
                ShowContextMenu();
                break;
        }
    }

    private void ShowContextMenu()
    {
        var loc = LocalizationService.Instance;
        var hMenu = CreatePopupMenu();
        if (hMenu == IntPtr.Zero) return;

        AppendMenu(hMenu, MF_STRING, (IntPtr)ID_SHOW, loc["TrayShow"]);
        AppendMenu(hMenu, MF_STRING, (IntPtr)ID_CHECK_UPDATES, loc["TrayCheckUpdates"]);
        if (_isAutostartEnabled is not null)
        {
            var flags = MF_STRING | (_isAutostartEnabled() ? MF_CHECKED : 0);
            AppendMenu(hMenu, flags, (IntPtr)ID_AUTOSTART, loc["TrayAutostart"]);
        }
        AppendMenu(hMenu, MF_SEPARATOR, IntPtr.Zero, "");
        AppendMenu(hMenu, MF_STRING, (IntPtr)ID_EXIT, loc["TrayExit"]);

        GetCursorPos(out var pt);
        SetForegroundWindow(_nid.hWnd);
        var result = TrackPopupMenu(hMenu, TPM_RIGHTBUTTON | TPM_RETURNCMD, pt.X, pt.Y, 0, _nid.hWnd, IntPtr.Zero);
        DestroyMenu(hMenu);

        switch (result)
        {
            case ID_SHOW: ShowRequested?.Invoke(); break;
            case ID_CHECK_UPDATES: CheckUpdatesRequested?.Invoke(); break;
            case ID_AUTOSTART: AutostartToggleRequested?.Invoke(); break;
            case ID_EXIT: ExitRequested?.Invoke(); break;
        }
    }

    private static void EnableDarkModeForMenus()
    {
        // undocumented uxtheme.dll APIs used by Windows 11 Settings (Windows 10 1903+)
        try { SetPreferredAppMode(1); } catch { }
    }

    private void ApplyDarkModeToWindow()
    {
        if (_messageWindow == IntPtr.Zero) return;
        try
        {
            var allow = 1;
            DwmSetWindowAttribute(_messageWindow, DWMWA_USE_IMMERSIVE_DARK_MODE, ref allow, sizeof(int));
        }
        catch { }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Shell_NotifyIconW(uint dwMessage, ref NOTIFYICONDATAW lpData);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool AppendMenu(IntPtr hMenu, uint uFlags, IntPtr uIDNewItem, string lpNewItem);

    [DllImport("user32.dll")]
    private static extern bool DestroyMenu(IntPtr hMenu);

    [DllImport("user32.dll")]
    private static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern int TrackPopupMenu(IntPtr hMenu, uint uFlags, int x, int y, int nReserved, IntPtr hWnd, IntPtr prcRect);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern ushort RegisterClassW(ref WNDCLASSW lpWndClass);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateWindowEx(uint dwExStyle, string lpClassName, string lpWindowName, uint dwStyle, int x, int y, int nWidth, int nHeight, IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

    [DllImport("user32.dll")]
    private static extern IntPtr DefWindowProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("uxtheme.dll", EntryPoint = "#135", SetLastError = true)]
    private static extern int SetPreferredAppMode(int appMode);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);
}
