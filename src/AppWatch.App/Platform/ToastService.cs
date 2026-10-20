using System;
using System.Diagnostics;
using AppWatch.Core.Abstractions;
using AppWatch.Infrastructure;
using AppWatch.Localization;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;

namespace AppWatch.Platform;

/// <summary>
/// App notifications via Windows App SDK. The previous implementation used the legacy
/// Windows.UI.Notifications.ToastNotificationManager, which silently no-ops for
/// unpackaged WinUI 3 apps (no registered AUMID shortcut).
/// Toast activation (click / buttons) is handled in App.OnLaunched via
/// AppNotificationManager.NotificationInvoked.
/// </summary>
public sealed class ToastService : IToastService
{
    private readonly IConfigStore _configStore;

    public ToastService(IConfigStore configStore) => _configStore = configStore;

    /// <summary>User gate; read fresh so the Settings toggle applies without restart.</summary>
    private bool Enabled => _configStore.Load().ToastNotificationsEnabled;

    public void ShowUpdateAvailable(string appName, string version)
    {
        if (!Enabled) return;
        try
        {
            var loc = LocalizationService.Instance;
            var notification = new AppNotificationBuilder()
                .AddText(loc["ToastUpdateTitle"])
                .AddText(string.Format(loc["ToastUpdateBody"], appName, version))
                .AddButton(new AppNotificationButton(loc["ToastBtnOpen"])
                    .AddArgument("action", "open"))
                .AddButton(new AppNotificationButton(loc["ToastBtnUpdate"])
                    .AddArgument("action", "update")
                    .AddArgument("app", appName))
                .BuildNotification();
            AppNotificationManager.Default.Show(notification);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Toast] Failed: {ex.Message}");
        }
    }

    public void ShowSummary(int count)
    {
        if (!Enabled) return;
        try
        {
            var loc = LocalizationService.Instance;
            var notification = new AppNotificationBuilder()
                .AddText(loc["ToastSummaryTitle"])
                .AddText(string.Format(loc["ToastSummaryBody"], count))
                .AddButton(new AppNotificationButton(loc["ToastBtnOpen"])
                    .AddArgument("action", "open"))
                .BuildNotification();
            AppNotificationManager.Default.Show(notification);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Toast] Failed: {ex.Message}");
        }
    }
}