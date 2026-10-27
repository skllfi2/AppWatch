using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;

class Program
{
    static int Main(string[] args)
    {
        // Force English culture BEFORE any other code runs
        var culture = CultureInfo.GetCultureInfo("en-US");
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;

        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var realCompiler = Path.Combine(baseDir, "XamlCompiler.exe.real");
        
        if (!File.Exists(realCompiler))
        {
            var nugetPaths = new[]
            {
                @"C:\Users\dexox\.nuget\packages\microsoft.windowsappsdk.winui\2.3.9\tools\net472\XamlCompiler.exe.real",
                @"C:\Users\dexox\.nuget\packages\microsoft.windowsappsdk.winui\2.3.10-experimental\tools\net472\XamlCompiler.exe.real",
            };
            foreach (var p in nugetPaths)
            {
                if (File.Exists(p))
                {
                    realCompiler = p;
                    break;
                }
            }
        }

        if (!File.Exists(realCompiler))
        {
            Console.Error.WriteLine("XamlCompiler.exe.real not found at: " + realCompiler);
            return 1;
        }

        Console.Error.WriteLine($"[XamlCompilerWrapper] Running {realCompiler} with args: {string.Join(" ", args)}");

        var psi = new ProcessStartInfo
        {
            FileName = realCompiler,
            Arguments = string.Join(" ", args.Select(a => a.Contains(' ') ? "\"" + a + "\"" : a)),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = Environment.CurrentDirectory,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8,
        };

        using var proc = Process.Start(psi);
        proc.OutputDataReceived += (s, e) => { if (e.Data != null) Console.WriteLine(e.Data); };
        proc.ErrorDataReceived += (s, e) => { if (e.Data != null) Console.Error.WriteLine(e.Data); };
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();
        proc.WaitForExit();
        return proc.ExitCode;
    }
}