using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Streams;

namespace AppWatch.Platform;

/// <summary>
/// Extracts app icons asynchronously:
/// - Win32 (.exe): SHGetFileInfo via P/Invoke → HICON → BitmapImage
/// - UWP (PackageFamilyName): PackageManager.GetAppListEntriesAsync() → GetThumbnailAsync()
/// Returns BitmapImage ready for Image.Source binding.
/// </summary>
public static class IconExtractor
{
    // === Win32 P/Invoke (traditional DllImport) ===
    private const int SHGFI_ICON = 0x100;
    private const int SHGFI_LARGEICON = 0x0;
    private const int SHGFI_SMALLICON = 0x1;
    private const int SHGFI_USEFILEATTRIBUTES = 0x10;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr SHGetFileInfoW(
        string pszPath,
        uint dwFileAttributes,
        ref SHFILEINFO psfi,
        uint cbSizeFileInfo,
        uint uFlags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [SupportedOSPlatform("windows10.0.17763.0")]
    public static async Task<BitmapImage?> GetIconAsync(InstalledApp app, int size = 48)
    {
        // UWP app: use PackageFamilyName to get logo
        if (!string.IsNullOrEmpty(app.PackageFamilyName))
        {
            return await GetUwpIconAsync(app.PackageFamilyName, size);
        }

        // Win32 app: extract from executable
        if (!string.IsNullOrEmpty(app.ExecutablePath) && File.Exists(app.ExecutablePath))
        {
            return await GetWin32IconAsync(app.ExecutablePath, size);
        }

        return null;
    }

    /// <summary>
    /// Extracts icon from Win32 executable using SHGetFileInfo.
    /// Returns BitmapImage (48x48 by default, or 16x16 for small).
    /// </summary>
    [SupportedOSPlatform("windows10.0.17763.0")]
    private static async Task<BitmapImage?> GetWin32IconAsync(string exePath, int size)
    {
        try
        {
            var shfi = new SHFILEINFO();
            uint flags = (uint)(SHGFI_ICON | SHGFI_USEFILEATTRIBUTES | (size <= 16 ? SHGFI_SMALLICON : SHGFI_LARGEICON));
            IntPtr result = SHGetFileInfoW(exePath, 0, ref shfi, (uint)Marshal.SizeOf<SHFILEINFO>(), flags);

            if (result == IntPtr.Zero || shfi.hIcon == IntPtr.Zero)
                return null;

            // Convert HICON to BitmapImage
            var bitmap = await IconToBitmapImageAsync(shfi.hIcon, size);
            DestroyIcon(shfi.hIcon); // free unmanaged resource
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Converts HICON to BitmapImage on UI thread.
    /// </summary>
    [SupportedOSPlatform("windows10.0.17763.0")]
    private static async Task<BitmapImage?> IconToBitmapImageAsync(IntPtr hIcon, int size)
    {
        // Must run on UI thread for BitmapImage creation
        var tcs = new TaskCompletionSource<BitmapImage?>();
        var queue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        if (!queue.TryEnqueue(() =>
        {
            try
            {
                using var icon = System.Drawing.Icon.FromHandle(hIcon);
                using var bitmap = icon.ToBitmap();
                using var ms = new MemoryStream();
                bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                ms.Position = 0;

                var img = new BitmapImage();
                var stream = ms.AsRandomAccessStream();
                img.SetSourceAsync(stream).AsTask().ContinueWith(t =>
                {
                    if (t.IsFaulted) tcs.SetException(t.Exception!);
                    else tcs.SetResult(img);
                });
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        }))
        {
            tcs.SetResult(null);
        }
        return await tcs.Task;
    }

    /// <summary>
    /// Gets UWP app logo/thumbnail via PackageManager.
    /// </summary>
    [SupportedOSPlatform("windows10.0.17763.0")]
    private static async Task<BitmapImage?> GetUwpIconAsync(string packageFamilyName, int size)
    {
        try
        {
            var pm = new Windows.Management.Deployment.PackageManager();
            var package = pm.FindPackage(packageFamilyName);
            if (package == null) return null;

            // Get the AppListEntry for the main app
            var entries = await package.GetAppListEntriesAsync();
            var entry = entries.FirstOrDefault();
            if (entry == null) return null;

            // Try to get thumbnail - API may vary by SDK version
            var thumbnail = await GetThumbnailFromEntry(entry);
            if (thumbnail == null) return null;

            var img = new BitmapImage();
            using var stream = await thumbnail.OpenReadAsync();
            await img.SetSourceAsync(stream);
            return img;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Gets thumbnail from AppListEntry using reflection (API varies by SDK).
    /// </summary>
    private static async Task<IRandomAccessStreamReference?> GetThumbnailFromEntry(object entry)
    {
        try
        {
            // Try GetThumbnailAsync method if available
            var method = entry.GetType().GetMethod("GetThumbnailAsync");
            if (method != null)
            {
                var task = (Task<IRandomAccessStreamReference?>)method.Invoke(entry, null)!;
                return await task;
            }
        }
        catch { }
        return null;
    }
}