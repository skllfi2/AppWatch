// Ported from ZuiV2 Platform/AdminElevation.cs (our own code).
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using AppWatch.Core.Abstractions;

namespace AppWatch.Infrastructure.System;

public sealed class ElevationService : IElevationService
{
    public bool IsElevated => IsRunningAsAdmin();

    public bool RunElevated(string executablePath, string arguments) =>
        ShellExecuteW(IntPtr.Zero, "runas", executablePath, arguments, null, 1).ToInt64() > 32;

    public bool LaunchAsAdmin(string executablePath, string? arguments = null) =>
        ShellExecuteW(IntPtr.Zero, "runas", executablePath, arguments, null, 1).ToInt64() > 32;

    /// <summary>
    /// Distinguishes a fully elevated admin token from a filtered one
    /// (admin user without elevation) via TOKEN_ELEVATION_TYPE.
    /// </summary>
    public static bool IsRunningAsAdmin()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            if (!principal.IsInRole(WindowsBuiltInRole.Administrator))
                return false;

            if (!OpenProcessToken(Process.GetCurrentProcess().Handle, 0x8 /* TOKEN_QUERY */, out var tokenHandle))
                return false;
            try
            {
                var buf = Marshal.AllocHGlobal(4);
                try
                {
                    if (!GetTokenInformation(tokenHandle, 18 /* TokenElevationType */, buf, 4, out var returnLength)
                        || returnLength < 4)
                        return false;
                    return Marshal.ReadInt32(buf) == 2; // TokenElevationTypeFull
                }
                finally
                {
                    Marshal.FreeHGlobal(buf);
                }
            }
            finally
            {
                if (tokenHandle != IntPtr.Zero) CloseHandle(tokenHandle);
            }
        }
        catch
        {
            return false;
        }
    }

    [DllImport("shell32.dll", SetLastError = true)]
    private static extern IntPtr ShellExecuteW(
        IntPtr hwnd,
        [MarshalAs(UnmanagedType.LPWStr)] string lpOperation,
        [MarshalAs(UnmanagedType.LPWStr)] string lpFile,
        [MarshalAs(UnmanagedType.LPWStr)] string? lpParameters,
        [MarshalAs(UnmanagedType.LPWStr)] string? lpDirectory,
        int nShowCmd);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool OpenProcessToken(IntPtr processHandle, uint desiredAccess, out IntPtr tokenHandle);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool GetTokenInformation(
        IntPtr tokenHandle, int tokenInformationClass,
        IntPtr tokenInformation, int tokenInformationLength, out int returnLength);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr hObject);
}
