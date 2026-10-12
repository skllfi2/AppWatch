using System.Diagnostics;
using AppWatch.Core.Abstractions;

namespace AppWatch.Infrastructure.System;

public sealed class ProcessService : IProcessService
{
    public bool IsRunning(string processName) =>
        Process.GetProcessesByName(processName).Length > 0;

    public void Kill(string processName)
    {
        foreach (var process in Process.GetProcessesByName(processName))
        {
            try
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(10_000);
            }
            finally
            {
                process.Dispose();
            }
        }
    }
}
