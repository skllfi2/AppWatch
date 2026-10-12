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
            WinRT.ComWrappersSupport.InitializeComWrappers();

            Microsoft.UI.Xaml.Application.Start(_ =>
            {
                var context = new Microsoft.UI.Dispatching.DispatcherQueueSynchronizationContext(
                    Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread());
                System.Threading.SynchronizationContext.SetSynchronizationContext(context);
                new App();
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
