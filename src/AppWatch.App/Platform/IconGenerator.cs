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
        try
        {
            using var bmp = new System.Drawing.Bitmap(size, size);
            using (var g = System.Drawing.Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                var c = System.Drawing.Color.FromArgb((int)color);
                using var brush = new System.Drawing.SolidBrush(c);
                g.FillEllipse(brush, 1, 1, size - 2, size - 2);
            }
            return bmp.GetHicon();
        }
        catch
        {
            return nint.Zero;
        }
    }

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern nint LoadImage(nint hinst, string lpszName, uint uType, int cxDesired, int cyDesired, uint fuLoad);

    private const uint IMAGE_ICON = 1;
    private const uint LR_LOADFROMFILE = 0x0010;

    public static nint LoadIconFromFile(string path, int size = 16)
    {
        if (System.IO.File.Exists(path))
        {
            var h = LoadImage(nint.Zero, path, IMAGE_ICON, size, size, LR_LOADFROMFILE);
            if (h != nint.Zero) return h;
        }
        return CreateDefaultIcon();
    }

    public static nint GetAppIcon(int size = 16)
    {
        var path = System.IO.Path.Combine(System.AppContext.BaseDirectory, "Assets", "AppIcon.ico");
        return LoadIconFromFile(path, size);
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
