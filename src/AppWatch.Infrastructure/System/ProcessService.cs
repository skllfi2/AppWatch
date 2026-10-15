using System.ComponentModel;
using System.Diagnostics;
using AppWatch.Core.Abstractions;
using Serilog;

namespace AppWatch.Infrastructure.System;

public sealed class ProcessService : IProcessService
{
    public bool IsRunning(string processName) =>
        Process.GetProcessesByName(processName).Length > 0;

    // P0 live-fix: killing by name can meet an elevated process (real case: user's own
    // winws DPI-bypass runs as admin; non-elevated AppWatch gets Win32Exception(5)
    // "Отказано в доступе"). Tolerate per-process failures, keep killing the rest,
    // and REPORT success so the engine can abort the update with a clear reason
    // instead of crashing with a raw access-denied.
    public bool Kill(string processName)
    {
        var allKilled = true;
        foreach (var process in Process.GetProcessesByName(processName))
        {
            try
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(10_000);
                }
                catch (Win32Exception ex)
                {
                    Log.Information("kill of {Process} pid {Pid} failed: {Error}",
                        processName, process.Id, ex.Message);
                    allKilled = false;
                }
                catch (InvalidOperationException) { /* already exited */ }
                catch (SystemException) { allKilled = false; }
            }
            finally
            {
                process.Dispose();
            }
        }
        return allKilled;
    }
}
