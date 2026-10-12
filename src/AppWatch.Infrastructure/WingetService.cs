using System.Diagnostics;
using System.Text;
using AppWatch.Core.Domain;
using Serilog;

namespace AppWatch.Infrastructure;

public sealed class WingetService
{
    private static readonly string WingetPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Microsoft", "WindowsApps", "winget.exe");

    public bool IsAvailable => File.Exists(WingetPath);

    public async Task<IReadOnlyList<WingetPackageUpdate>> GetUpgradesAsync(CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
            throw new InvalidOperationException("winget is not available");
        var output = await RunWingetAsync(
            "upgrade --include-unknown --disable-interactivity --accept-source-agreements",
            cancellationToken);
        return WingetOutputParser.ParseUpgrades(output);
    }

    /// <summary>
    /// Updates one package. Progress values: 0–100 = download phase, 100–200 = install phase
    /// (winget restarts the progress bar for install; the service tags install-phase values
    /// with +100 so the UI can map them).
    /// </summary>
    public async Task<(bool Ok, string Message)> UpgradePackageAsync(
        string packageId, string packageName,
        IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
            throw new InvalidOperationException("winget is not available");

        var lastMessage = string.Empty;
        var arguments =
            $"upgrade --id \"{packageId}\" --exact --include-unknown --disable-interactivity --accept-source-agreements --accept-package-agreements";

        var exitCode = await RunUpgradeAsync(arguments);
        // 0x8A15005F: package requires an explicit install location (e.g. Blizzard.BattleNet)
        if (exitCode == unchecked((int)0x8A15005F))
        {
            var location = TryFindInstallLocation(packageName);
            if (location is not null)
            {
                Log.Information("winget upgrade --id {Id}: retrying with install location {Location}", packageId, location);
                exitCode = await RunUpgradeAsync($"{arguments} --location \"{location}\"");
            }
        }

        Log.Information("winget upgrade --id {Id} finished with exit code {ExitCode}: {Message}",
            packageId, exitCode, lastMessage);
        return (exitCode == 0, lastMessage);

        async Task<int> RunUpgradeAsync(string args)
        {
            var lastPercent = 0.0;
            var installPhase = false;
            return await RunWingetStreamingAsync(
                args,
                line =>
                {
                    var trimmed = line.Trim();
                    if (trimmed.Length > 0 && trimmed.Replace("\0", string.Empty).Length > 0
                        && WingetOutputParser.TryParseProgressPercent(trimmed) is null)
                        lastMessage = trimmed;
                    if (WingetOutputParser.TryParseProgressPercent(line) is not { } percent) return;
                    if (percent < lastPercent) installPhase = true;
                    lastPercent = percent;
                    progress?.Report(installPhase ? 100 + percent : percent);
                },
                cancellationToken);
        }
    }

    /// <summary>
    /// Finds the install location registered in Add/Remove Programs for a display name,
    /// so winget packages that demand --location can be upgraded non-interactively.
    /// </summary>
    private static string? TryFindInstallLocation(string displayName)
    {
        var roots = new[]
        {
            Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
            Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"),
            Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
        };
        foreach (var root in roots)
        {
            if (root is null) continue;
            using var _ = root;
            foreach (var subKeyName in root.GetSubKeyNames())
            {
                using var key = root.OpenSubKey(subKeyName);
                if (key?.GetValue("DisplayName") is not string name) continue;
                if (!string.Equals(name.Trim(), displayName?.Trim(), StringComparison.OrdinalIgnoreCase)) continue;
                if (key.GetValue("InstallLocation") is string loc && !string.IsNullOrWhiteSpace(loc))
                    return loc;
            }
        }
        return null;
    }

    public async Task<string> InstallAsync(string packageId, CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
            throw new InvalidOperationException("winget is not available");
        return await RunWingetAsync(
            $"install --id \"{packageId}\" --exact --silent --accept-source-agreements --accept-package-agreements",
            cancellationToken);
    }

    private async Task<string> RunWingetAsync(string arguments, CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo
        {
            FileName = WingetPath,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
        };

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("failed to start winget");
        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        Log.Information("winget '{Arguments}' finished with exit code {ExitCode}", arguments, process.ExitCode);
        return output;
    }

    private async Task<int> RunWingetStreamingAsync(
        string arguments, Action<string>? onLine, CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo
        {
            FileName = WingetPath,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
        };

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("failed to start winget");
        _ = process.StandardError.ReadToEndAsync(cancellationToken);
        while (await process.StandardOutput.ReadLineAsync(cancellationToken) is { } line)
            onLine?.Invoke(line);
        await process.WaitForExitAsync(cancellationToken);
        return process.ExitCode;
    }
}
