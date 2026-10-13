using System;
using AppWatch.Infrastructure;

namespace AppWatch.Platform;

public sealed class ToastService : IToastService
{
    public void ShowUpdateAvailable(string appName, string version)
    {
        try
        {
            var notifier = Windows.UI.Notifications.ToastNotificationManager.CreateToastNotifier("AppWatch");
            var xml = $"""
                <toast launch="appName={Uri.EscapeDataString(appName)}">
                    <visual>
                        <binding template="ToastGeneric">
                            <text>AppWatch: обновление найдено</text>
                            <text>{appName} — {version}</text>
                        </binding>
                    </visual>
                    <actions>
                        <action content="Открыть" arguments="openApp={Uri.EscapeDataString(appName)}"/>
                        <action content="Обновить" arguments="updateApp={Uri.EscapeDataString(appName)}"/>
                    </actions>
                </toast>
                """;

            var doc = new Windows.Data.Xml.Dom.XmlDocument();
            doc.LoadXml(xml);
            var toast = new Windows.UI.Notifications.ToastNotification(doc);
            notifier.Show(toast);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Toast] Failed: {ex.Message}");
        }
    }

    public void ShowSummary(int count)
    {
        try
        {
            var notifier = Windows.UI.Notifications.ToastNotificationManager.CreateToastNotifier("AppWatch");
            var xml = $"""
                <toast>
                    <visual>
                        <binding template="ToastGeneric">
                            <text>AppWatch: проверка завершена</text>
                            <text>Найдено {count} обновлений</text>
                        </binding>
                    </visual>
                </toast>
                """;

            var doc = new Windows.Data.Xml.Dom.XmlDocument();
            doc.LoadXml(xml);
            var toast = new Windows.UI.Notifications.ToastNotification(doc);
            notifier.Show(toast);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Toast] Failed: {ex.Message}");
        }
    }
}