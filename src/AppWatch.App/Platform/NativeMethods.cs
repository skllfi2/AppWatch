using System.Runtime.InteropServices;

namespace AppWatch.Platform;

internal static class NativeMethods
{
    public const int SwRestore = 9;

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);
}
