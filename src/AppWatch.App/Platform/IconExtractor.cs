using System;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Streams;

namespace AppWatch.Platform;

/// <summary>
/// Extracts app icons asynchronously with zero GDI leaks and bounded memory caching:
/// - Win32 (.exe): SHGetFileInfo via P/Invoke → HICON → converted to PNG bytes on worker thread → BitmapImage on UI thread
/// - UWP (PackageFamilyName): PackageManager.GetAppListEntriesAsync() → GetThumbnailAsync()
/// Returns BitmapImage ready for Image.Source binding.
/// </summary>
public static class IconExtractor
{
    private const int MaxCacheSize = 150;
    private static readonly ConcurrentDictionary<string, BitmapImage> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentQueue<string> CacheKeys = new();

    /// <summary>
    /// Clears the in-memory icon cache so updated icons can be reloaded.
    /// </summary>
    public static void ClearCache()
    {
        Cache.Clear();
        while (CacheKeys.TryDequeue(out _)) { }
    }

    private static void AddToCache(string key, BitmapImage image)
    {
        Cache[key] = image;
        CacheKeys.Enqueue(key);
        while (CacheKeys.Count > MaxCacheSize && CacheKeys.TryDequeue(out var oldKey))
        {
            Cache.TryRemove(oldKey, out _);
        }
    }

    [SupportedOSPlatform("windows10.0.17763.0")]
    public static async Task<BitmapImage?> GetIconFromExecutableAsync(string exePath, int size = 48)
    {
        if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath)) return null;

        var key = $"{exePath}:{size}";
        if (Cache.TryGetValue(key, out var cached))
            return cached;

        var icon = await GetWin32IconAsync(exePath, size);
        if (icon is not null)
            AddToCache(key, icon);
        return icon;
    }

    // === Win32 P/Invoke (traditional DllImport) ===
    private const int SHGFI_ICON = 0x100;
    private const int SHGFI_LARGEICON = 0x0;
    private const int SHGFI_SMALLICON = 0x1;

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
    /// Converts an unmanaged HICON to PNG bytes on the current worker thread.
    /// </summary>
    private static byte[]? IconHandleToPngBytes(IntPtr hIcon)
    {
        try
        {
            using var icon = System.Drawing.Icon.FromHandle(hIcon);
            using var bitmap = icon.ToBitmap();
            using var ms = new MemoryStream();
            bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            return ms.ToArray();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Extracts icon from Win32 executable using SHGetFileInfo.
    /// Safely cleans up unmanaged icon handles and returns BitmapImage.
    /// </summary>
    [SupportedOSPlatform("windows10.0.17763.0")]
    private static async Task<BitmapImage?> GetWin32IconAsync(string exePath, int size)
    {
        try
        {
            byte[]? pngBytes = null;
            var shfi = new SHFILEINFO();
            uint flags = (uint)(SHGFI_ICON | (size <= 16 ? SHGFI_SMALLICON : SHGFI_LARGEICON));
            IntPtr result = SHGetFileInfoW(exePath, 0, ref shfi, (uint)Marshal.SizeOf<SHFILEINFO>(), flags);

            if (result != IntPtr.Zero && shfi.hIcon != IntPtr.Zero)
            {
                try
                {
                    pngBytes = IconHandleToPngBytes(shfi.hIcon);
                }
                finally
                {
                    DestroyIcon(shfi.hIcon);
                }
            }

            // Fallback to System.Drawing.Icon.ExtractAssociatedIcon if SHGetFileInfo yielded nothing
            if (pngBytes is null && File.Exists(exePath))
            {
                try
                {
                    using var associatedIcon = System.Drawing.Icon.ExtractAssociatedIcon(exePath);
                    if (associatedIcon is not null)
                    {
                        using var bitmap = associatedIcon.ToBitmap();
                        using var ms = new MemoryStream();
                        bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                        pngBytes = ms.ToArray();
                    }
                }
                catch { }
            }

            if (pngBytes is not null && pngBytes.Length > 0)
            {
                return await PngBytesToBitmapImageAsync(pngBytes);
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Converts PNG bytes to BitmapImage safely on UI thread.
    /// </summary>
    [SupportedOSPlatform("windows10.0.17763.0")]
    private static async Task<BitmapImage?> PngBytesToBitmapImageAsync(byte[] pngBytes)
    {
        var queue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread()
                    ?? App.MainWindow?.DispatcherQueue;
        if (queue is null) return null;

        var tcs = new TaskCompletionSource<BitmapImage?>();
        if (!queue.TryEnqueue(async () =>
        {
            try
            {
                using var stream = new InMemoryRandomAccessStream();
                using (var writer = new DataWriter(stream.GetOutputStreamAt(0)))
                {
                    writer.WriteBytes(pngBytes);
                    await writer.StoreAsync();
                }
                stream.Seek(0);

                var img = new BitmapImage();
                await img.SetSourceAsync(stream);
                tcs.SetResult(img);
            }
            catch
            {
                tcs.SetResult(null);
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

            var entries = await package.GetAppListEntriesAsync();
            var entry = entries.Count > 0 ? entries[0] : null;
            if (entry == null) return null;

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

    private static async Task<IRandomAccessStreamReference?> GetThumbnailFromEntry(object entry)
    {
        try
        {
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
