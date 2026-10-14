using System.IO;
using AppWatch.Platform;
using Velopack;

namespace AppWatch;

public static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Any(a => a.Equals("--autostart-launch", StringComparison.OrdinalIgnoreCase)))
            App.StartHidden = true;

        if (!SingleInstance.TryAcquire())
        {
            _ = SingleInstance.NotifyExistingInstanceAsync("SHOW");
            return;
        }

        VelopackApp.Build().Run();

        var crashLogPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AppWatch", "logs", "crash.log");
        try
        {
            // Unpackaged app: load the WindowsAppSDK framework package explicitly.
            // Without this the first WinRT activation dies natively (combase E_FAIL 0x8000ffff).
            if (!Microsoft.Windows.ApplicationModel.DynamicDependency.Bootstrap.TryInitialize(
                    Microsoft.WindowsAppSDK.Release.MajorMinor,
                    out _))
            {
                throw new InvalidOperationException(
                    "WindowsAppSDK bootstrap failed: is the WindowsAppRuntime framework package installed?");
            }

            WinRT.ComWrappersSupport.InitializeComWrappers();

            Microsoft.UI.Xaml.Application.Start(_ =>
            {
                try
                {
                    var context = new Microsoft.UI.Dispatching.DispatcherQueueSynchronizationContext(
                        Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread());
                    System.Threading.SynchronizationContext.SetSynchronizationContext(context);
                    new App();
                }
                catch (Exception ex)
                {
                    // Stowed exceptions kill the process without unwinding to Main's catch —
                    // write the real cause here, inside the callback.
                    Directory.CreateDirectory(Path.GetDirectoryName(crashLogPath)!);
                    File.WriteAllText(crashLogPath + ".inner",
                        $"[{DateTime.Now}] FATAL (Start callback): {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}\n");
                    throw;
                }
            });
        }
        catch (Exception ex)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(crashLogPath)!);
            File.WriteAllText(crashLogPath,
                $"[{DateTime.Now}] FATAL: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}\n");
        }
        finally
        {
            SingleInstance.Stop();
        }
    }
}
