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

    public void ShowSummary(int count)
    {
        if (!Enabled) return;
        var loc = LocalizationService.Instance;
        var title = loc["ToastSummaryTitle"];
        var body = string.Format(loc["ToastSummaryBody"], count);

        try
        {
            App.Resolve<SoundService>().PlayNotification();
        }
        catch { }

        bool shown = false;
        try
        {
            var notification = new AppNotificationBuilder()
                .AddText(title)
                .AddText(body)
                .AddButton(new AppNotificationButton(loc["ToastBtnOpen"])
                    .AddArgument("action", "open"))
                .BuildNotification();
            AppNotificationManager.Default.Show(notification);
            shown = true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Toast] AppNotificationManager failed: {ex.Message}");
        }

        if (!shown)
        {
            // Unpackaged fallback: standard Windows Action Center balloon via TrayService
            try
            {
                var tray = App.Resolve<TrayService>();
                tray.ShowBalloon(title, body);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Toast] Tray balloon fallback failed: {ex.Message}");
            }
        }

        try
        {
            App.MainWindow?.DispatcherQueue?.TryEnqueue(() =>
            {
                try { App.Resolve<ViewModels.MainViewModel>().SyncFromConfig(); }
                catch { }
            });
        }
        catch { }
    }
}