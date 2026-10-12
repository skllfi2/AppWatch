using System.Diagnostics;
using AppWatch.Core.Abstractions;

namespace AppWatch.Infrastructure.System;

public sealed class AppLauncher : IAppLauncher
{
    public void Launch(string executablePath) =>
        Process.Start(new ProcessStartInfo(executablePath) { UseShellExecute = true });
}
