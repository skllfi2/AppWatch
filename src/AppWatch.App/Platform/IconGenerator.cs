using System.Runtime.InteropServices;

namespace AppWatch.Platform;

/// <summary>
/// Generates colored tray icons programmatically using Win32 GDI — no icon asset files needed.
/// Ported from ZuiV2 Platform/IconGenerator.cs (our own code).
/// </summary>
internal static class IconGenerator
{
    [DllImport("gdi32.dll")]
    private static extern nint CreateSolidBrush(uint crColor);

    [DllImport("gdi32.dll")]
    private static extern nint CreateRoundRectRgn(int x1, int y1, int x2, int y2, int w, int h);

    [DllImport("user32.dll")]
    private static extern nint CreateIconIndirect(ref ICONINFO icon);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(nint hObject);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [StructLayout(LayoutKind.Sequential)]
    private struct ICONINFO
    {
        public bool fIcon;
        public uint xHotspot;
        public uint yHotspot;
        public nint hbmMask;
        public nint hbmColor;
        public uint dwReserved;
    }

    /// <summary>Creates a colored tray icon. Color as 0xAARRGGBB.</summary>
    public static nint CreateIcon(uint color, int size = 16)
    {
        var hBrush = CreateSolidBrush(color);
        var hRgn = CreateRoundRectRgn(0, 0, size, size, size / 3, size / 3);
        var hMask = CreateSolidBrush(0x00FFFFFF);

        var icon = new ICONINFO
        {
            fIcon = true,
            hbmColor = hBrush,
            hbmMask = hMask,
        };

        var hIcon = CreateIconIndirect(ref icon);

        DeleteObject(hBrush);
        DeleteObject(hRgn);
        DeleteObject(hMask);

        return hIcon;
    }

    public static nint CreateRunningIcon() => CreateIcon(0xFF4CAF50);

    public static nint CreateStoppedIcon() => CreateIcon(0xFF9E9E9E);

    public static nint CreateErrorIcon() => CreateIcon(0xFFFB5E5E);

    public static nint CreateDefaultIcon() => CreateIcon(0xFF60CDFF);

    public static void Destroy(nint hIcon)
    {
        if (hIcon != IntPtr.Zero)
            DestroyIcon(hIcon);
    }
}
