using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Media.Imaging;
using AppWatch.Platform;

namespace AppWatch.ViewModels;

/// <summary>
/// Async icon loading helper for ViewModels.
/// Loads Win32/UWP icons in background and exposes BitmapImage for x:Bind.
/// </summary>
public static class IconHelper
{
    /// <summary>
    /// Loads icon for a tracked app. Priority:
    /// 1. GitHub avatar URL (AvatarUrl)
    /// 2. Win32 exe icon (via InstalledApp.ExecutablePath)
    /// 3. UWP logo (via InstalledApp.PackageFamilyName)
    /// 4. null (UI falls back to MonogramBrush)
    /// </summary>
    public static async Task<BitmapImage?> LoadIconAsync(TrackedAppItem item)
    {
        // 1. GitHub avatar URL (already in TrackedAppItem.AvatarUrl)
        if (!string.IsNullOrWhiteSpace(item.AvatarUrl))
        {
            try
            {
                var img = new BitmapImage(new Uri(item.AvatarUrl));
                return img;
            }
            catch { /* fall through to local icons */ }
        }

        // 2-3. Local icons require matching installed app
        // This would need access to InstalledApp list - skip for now
        // Can be added when "Installed Apps" page is implemented

        return null;
    }

    /// <summary>
    /// Loads icon for an installed app (Win32 or UWP).
    /// </summary>
    public static async Task<BitmapImage?> LoadIconAsync(InstalledApp app, int size = 48)
    {
        return await IconExtractor.GetIconAsync(app, size);
    }
}