// Ported from ZuiV2 Zui.Core/Services/AutostartService.cs (our own code) —
// app autostart via the HKCU\Run registry key.
using Microsoft.Win32;
using Serilog;

namespace AppWatch.Infrastructure;

public sealed class AutostartService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "AppWatch";

    public bool IsEnabled
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKey, false);
                return !string.IsNullOrWhiteSpace(key?.GetValue(AppName) as string);
            }
            catch { return false; }
        }
    }

    public bool SetEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, true);
            if (key is null) return false;

            if (enabled)
            {
                var exePath = Environment.ProcessPath ?? "";
                if (string.IsNullOrEmpty(exePath)) return false;
                key.SetValue(AppName, $"\"{exePath}\" --autostart-launch");
            }
            else
            {
                key.DeleteValue(AppName, false);
            }

            Log.Information("autostart {State}", enabled ? "enabled" : "disabled");
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "failed to set autostart");
            return false;
        }
    }
}
