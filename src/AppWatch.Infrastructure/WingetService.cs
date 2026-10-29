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

    /// <summary>
    /// Exit code: winget itself crashed with E_INVALIDARG. Seen when Microsoft's catalog data
    /// is broken (e.g. Microsoft.PowerShell overlapping version ranges, winget-cli#6613):
    /// `winget upgrade`/`list` die during package correlation for ALL users until the
    /// CDN data is fixed. Scoped commands (search / install --id / show) keep working.
    /// </summary>
    public const int CatalogBrokenExitCode = unchecked((int)0x80070057);

    public async Task<IReadOnlyList<WingetPackageUpdate>> GetUpgradesAsync(CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
            throw new InvalidOperationException("winget is not available");
        var output = await RunWingetAsync(
            "upgrade --include-unknown --disable-interactivity --accept-source-agreements",
            cancellationToken);
        if (LastExitCode == CatalogBrokenExitCode)
            throw new WingetCatalogBrokenException(output);
        return WingetOutputParser.ParseUpgrades(output);
    }

    /// <summary>
    /// Updates one package. winget with redirected stdout prints no progress bar
    /// (no percent/speed — verified live), but plain phase lines: Скачивание/хэш/
    /// установка. We report those as WingetPhase so the UI can show honest,
    /// live status instead of a stuck "Запуск…" and a dead bar.
    /// </summary>
    public async Task<(bool Ok, string Message)> UpgradePackageAsync(
        string packageId, string packageName,
        IProgress<WingetProgress>? progress = null, CancellationToken cancellationToken = default)
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
            var tempWatcher = new WingetTempDownloadWatcher(packageId);
            return await RunWingetStreamingAsync(
                args,
                line =>
                {
                    var trimmed = line.Trim();
                    if (trimmed.Length > 0 && trimmed.Replace("\0", string.Empty).Length > 0
                        && WingetOutputParser.TryParseProgressPercent(trimmed) is null
                        && WingetOutputParser.TryParsePhase(trimmed) is null)
                        lastMessage = trimmed;
                    if (WingetOutputParser.TryParsePhase(line) is not { } phase) return;
                    if (phase is WingetPhase.Downloading or WingetPhase.Downloaded)
                        tempWatcher.Start(cancellationToken);
                    else
                        tempWatcher.Stop();
                    progress?.Report(WingetProgress.Of(phase));
                },
                cancellationToken,
                () =>
                {
                    // heartbeat: while winget is silent (its download prints no lines at all),
                    // report bytes + speed from the growing temp folder
                    var snapshot = tempWatcher.Snapshot();
                    if (snapshot is { } s)
                        progress?.Report(new WingetProgress(WingetPhase.Downloading, s.Bytes, s.BytesPerSec));
                },
                heartbeatInterval: TimeSpan.FromMilliseconds(250));
        }
    }

    /// <summary>
    /// winget downloads installers to %TEMP%\{PackageId}_{Version}\ — while winget itself
    /// prints nothing, the growing folder is the only honest progress signal available.
    /// </summary>
    private sealed class WingetTempDownloadWatcher
    {
        private readonly string _packageId;
        private CancellationTokenSource? _cts;
        private Task? _poll;
        private long _lastBytes;
        private DateTimeOffset _lastAt;
        private string? _dir;

        public WingetTempDownloadWatcher(string packageId) => _packageId = packageId;

        public void Start(CancellationToken outer)
        {
            if (_poll is not null) return;
            _cts = CancellationTokenSource.CreateLinkedTokenSource(outer);
            _dir = null;
            _lastBytes = 0;
            _lastAt = DateTimeOffset.UtcNow;
            var token = _cts.Token;
            _poll = Task.Run(async () =>
            {
                var temp = Path.Combine(Path.GetTempPath(), "");
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        _dir ??= Directory.EnumerateDirectories(temp, $"*{_packageId}*")
                                                    .OrderByDescending(Directory.GetLastWriteTimeUtc)
                                                    .FirstOrDefault();
                        if (_dir is not null && Directory.Exists(_dir))
                        {
                            long bytes = 0;
                            foreach (var f in Directory.EnumerateFiles(_dir, "*", SearchOption.AllDirectories))
                            {
                                try { bytes += new FileInfo(f).Length; } catch (IOException) { }
                            }
                            _lastBytes = bytes;
                        }
                    }
                    catch { /* best effort */ }
                    await Task.Delay(200, token);
                }
            }, token);
        }

        public void Stop()
        {
            _cts?.Cancel();
            try { _poll?.Wait(100); } catch { }
            _cts?.Dispose();
            _cts = null;
            _poll = null;
        }

        public (long Bytes, double BytesPerSec)? Snapshot()
        {
            if (_cts is null || _poll is null) return null; // not running
            var now = DateTimeOffset.UtcNow;
            var dt = (now - _lastAt).TotalSeconds;
            var bps = dt > 0 ? _lastBytes / dt : 0;
            _lastAt = now;
            return (_lastBytes, bps);
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

    public async Task<IReadOnlyList<WingetSearchResult>> SearchAsync(
        string query, CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
            throw new InvalidOperationException("winget is not available");
        if (string.IsNullOrWhiteSpace(query))
            return [];
        var output = await RunWingetAsync(
            $"search \"{query.Trim()}\" --accept-source-agreements --disable-interactivity",
            cancellationToken);
        return WingetOutputParser.ParseSearch(output);
    }

    /// <summary>Exit code: winget reports "already installed" (APPINSTALLER_CLI_ERROR_PACKAGE_ALREADY_INSTALLED).</summary>
    public const int AlreadyInstalledExitCode = unchecked((int)0x8A150041);

    /// <summary>Uninstalls a winget package; success = exit code 0.</summary>
    public async Task<(int ExitCode, string Output)> UninstallAsync(
        string packageId, CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
            throw new InvalidOperationException("winget is not available");
        var output = await RunWingetAsync(
            $"uninstall --id \"{packageId}\" --exact --silent --disable-interactivity --accept-source-agreements",
            cancellationToken);
        return (LastExitCode, output);
    }

    public async Task<(int ExitCode, string Output)> InstallAsync(
        string packageId, CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
            throw new InvalidOperationException("winget is not available");
        var output = await RunWingetAsync(
            $"install --id \"{packageId}\" --exact --silent --accept-source-agreements --accept-package-agreements --disable-interactivity",
            cancellationToken);
        return (LastExitCode, output);
    }

    private int LastExitCode { get; set; }

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
        LastExitCode = process.ExitCode;
        Log.Information("winget '{Arguments}' finished with exit code {ExitCode}", arguments, process.ExitCode);
        return output;
    }

    private async Task<int> RunWingetStreamingAsync(
        string arguments, Action<string>? onLine, CancellationToken cancellationToken,
        Action? heartbeat = null, TimeSpan heartbeatInterval = default)
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

        using var heartbeatCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (heartbeat is not null && heartbeatInterval > TimeSpan.Zero)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    while (!heartbeatCts.Token.IsCancellationRequested)
                    {
                        await Task.Delay(heartbeatInterval, heartbeatCts.Token);
                        heartbeat();
                    }
                }
                catch (OperationCanceledException) { /* expected on exit */ }
            }, heartbeatCts.Token);
        }

        while (await process.StandardOutput.ReadLineAsync(cancellationToken) is { } line)
            onLine?.Invoke(line);
        await process.WaitForExitAsync(cancellationToken);
        heartbeatCts.Cancel();
        return process.ExitCode;
    }
}
