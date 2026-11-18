using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AppWatch.Core.Domain;
using Serilog;

namespace AppWatch.Infrastructure.System;

/// <summary>
/// Scans system package managers (npm, bun, dotnet tool, cargo, scoop, chocolatey) for installed packages
/// and checks for outdated versions.
/// </summary>
public sealed class PackageToolScanner
{
    private readonly ConsoleToolRunner _runner;

    public PackageToolScanner(ConsoleToolRunner? runner = null)
    {
        _runner = runner ?? new ConsoleToolRunner();
    }

    public async Task<IReadOnlyList<PackageToolUpdate>> ScanAllAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<PackageToolUpdate>();

        // Dotnet global tools
        try
        {
            var dotnetTools = await ScanDotnetToolsAsync(cancellationToken).ConfigureAwait(false);
            results.AddRange(dotnetTools);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Failed to scan dotnet global tools");
        }

        // NPM global packages
        try
        {
            var npmTools = await ScanNpmGlobalPackagesAsync(cancellationToken).ConfigureAwait(false);
            results.AddRange(npmTools);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Failed to scan npm global packages");
        }

        // Bun global packages
        try
        {
            var bunTools = await ScanBunGlobalPackagesAsync(cancellationToken).ConfigureAwait(false);
            results.AddRange(bunTools);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Failed to scan bun global packages");
        }

        // Cargo tools
        try
        {
            var cargoTools = await ScanCargoPackagesAsync(cancellationToken).ConfigureAwait(false);
            results.AddRange(cargoTools);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Failed to scan cargo packages");
        }

        // Scoop packages
        try
        {
            var scoopTools = await ScanScoopPackagesAsync(cancellationToken).ConfigureAwait(false);
            results.AddRange(scoopTools);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Failed to scan scoop packages");
        }

        // Chocolatey packages
        try
        {
            var chocoTools = await ScanChocolateyPackagesAsync(cancellationToken).ConfigureAwait(false);
            results.AddRange(chocoTools);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Failed to scan chocolatey packages");
        }

        return results;
    }

    public async Task<IReadOnlyList<PackageToolUpdate>> ScanDotnetToolsAsync(CancellationToken cancellationToken = default)
    {
        var probe = _runner.ProbeRunner(PackageRunnerType.DotnetTool);
        if (!probe.IsAvailable || probe.ExecutablePath is null)
            return Array.Empty<PackageToolUpdate>();

        var (exitCode, stdout, _) = await RunProcessAsync(probe.ExecutablePath, "tool list -g", cancellationToken).ConfigureAwait(false);
        if (exitCode != 0 || string.IsNullOrWhiteSpace(stdout))
            return Array.Empty<PackageToolUpdate>();

        return ParseDotnetToolsOutput(stdout);
    }

    public static IReadOnlyList<PackageToolUpdate> ParseDotnetToolsOutput(string stdout)
    {
        var list = new List<PackageToolUpdate>();
        var lines = stdout.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        
        bool dataStarted = false;
        foreach (var line in lines)
        {
            if (line.StartsWith("---", StringComparison.Ordinal))
            {
                dataStarted = true;
                continue;
            }

            if (!dataStarted)
                continue;

            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                var packageId = parts[0];
                var version = parts[1];
                var command = parts.Length >= 3 ? parts[2] : packageId;

                list.Add(new PackageToolUpdate(
                    PackageRunnerType.DotnetTool,
                    packageId,
                    version,
                    version,
                    command
                ));
            }
        }

        return list;
    }

    public async Task<IReadOnlyList<PackageToolUpdate>> ScanNpmGlobalPackagesAsync(CancellationToken cancellationToken = default)
    {
        var probe = _runner.ProbeRunner(PackageRunnerType.Npm);
        if (!probe.IsAvailable || probe.ExecutablePath is null)
            return Array.Empty<PackageToolUpdate>();

        var (_, stdout, _) = await RunProcessAsync(probe.ExecutablePath, "-g outdated --json", cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(stdout))
            return Array.Empty<PackageToolUpdate>();

        return ParseNpmOutdatedJson(stdout);
    }

    public static IReadOnlyList<PackageToolUpdate> ParseNpmOutdatedJson(string json)
    {
        var list = new List<PackageToolUpdate>();
        try
        {
            // npm may output stderr/text before or after JSON (e.g. npm error ENOENT...)
            var trimmed = json.Trim();
            int firstBrace = trimmed.IndexOf('{');
            int lastBrace = trimmed.LastIndexOf('}');
            if (firstBrace >= 0 && lastBrace > firstBrace)
            {
                trimmed = trimmed.Substring(firstBrace, lastBrace - firstBrace + 1);
            }

            using var doc = JsonDocument.Parse(trimmed);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return list;

            if (doc.RootElement.TryGetProperty("error", out _))
                return list;

            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                var pkgName = prop.Name;
                if (string.Equals(pkgName, "error", StringComparison.OrdinalIgnoreCase) ||
                    pkgName.StartsWith('_') ||
                    pkgName.StartsWith('$'))
                {
                    continue;
                }

                if (prop.Value.ValueKind == JsonValueKind.Object)
                {
                    var current = prop.Value.TryGetProperty("current", out var curElem) ? curElem.GetString() ?? "?" : "?";
                    var latest = prop.Value.TryGetProperty("latest", out var latElem) ? latElem.GetString() ?? current : current;

                    list.Add(new PackageToolUpdate(
                        PackageRunnerType.Npm,
                        pkgName,
                        current,
                        latest
                    ));
                }
            }
        }
        catch
        {
        }

        return list;
    }

    public async Task<IReadOnlyList<PackageToolUpdate>> ScanBunGlobalPackagesAsync(CancellationToken cancellationToken = default)
    {
        var probe = _runner.ProbeRunner(PackageRunnerType.Bun);
        if (!probe.IsAvailable || probe.ExecutablePath is null)
            return Array.Empty<PackageToolUpdate>();

        var (exitCode, stdout, _) = await RunProcessAsync(probe.ExecutablePath, "pm ls -g", cancellationToken).ConfigureAwait(false);
        if (exitCode != 0 || string.IsNullOrWhiteSpace(stdout))
            return Array.Empty<PackageToolUpdate>();

        return ParseBunListOutput(stdout);
    }

    public static IReadOnlyList<PackageToolUpdate> ParseBunListOutput(string stdout)
    {
        var list = new List<PackageToolUpdate>();
        var lines = stdout.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var line in lines)
        {
            var clean = line.TrimStart('└', '├', '─', ' ', '│');
            if (string.IsNullOrWhiteSpace(clean))
                continue;

            int atIndex = clean.LastIndexOf('@');
            if (atIndex > 0)
            {
                var pkgName = clean.Substring(0, atIndex);
                var version = clean.Substring(atIndex + 1);

                list.Add(new PackageToolUpdate(
                    PackageRunnerType.Bun,
                    pkgName,
                    version,
                    version
                ));
            }
        }

        return list;
    }

    public async Task<IReadOnlyList<PackageToolUpdate>> ScanCargoPackagesAsync(CancellationToken cancellationToken = default)
    {
        var probe = _runner.ProbeRunner(PackageRunnerType.Cargo);
        if (!probe.IsAvailable || probe.ExecutablePath is null)
            return Array.Empty<PackageToolUpdate>();

        var (exitCode, stdout, _) = await RunProcessAsync(probe.ExecutablePath, "install --list", cancellationToken).ConfigureAwait(false);
        if (exitCode != 0 || string.IsNullOrWhiteSpace(stdout))
            return Array.Empty<PackageToolUpdate>();

        var list = new List<PackageToolUpdate>();
        var lines = stdout.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var line in lines)
        {
            if (line.Contains(':') && line.Contains('v'))
            {
                var colonIdx = line.IndexOf(':');
                var header = line.Substring(0, colonIdx).Trim();
                var parts = header.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                {
                    var name = parts[0];
                    var ver = parts[1].TrimStart('v');
                    list.Add(new PackageToolUpdate(PackageRunnerType.Cargo, name, ver, ver));
                }
            }
        }
        return list;
    }

    public async Task<IReadOnlyList<PackageToolUpdate>> ScanScoopPackagesAsync(CancellationToken cancellationToken = default)
    {
        var probe = _runner.ProbeRunner(PackageRunnerType.Scoop);
        if (!probe.IsAvailable || probe.ExecutablePath is null)
            return Array.Empty<PackageToolUpdate>();

        var (_, statusOut, _) = await RunProcessAsync(probe.ExecutablePath, "status", cancellationToken).ConfigureAwait(false);
        var outdated = ParseScoopStatusOutput(statusOut);

        var (listCode, listOut, _) = await RunProcessAsync(probe.ExecutablePath, "list", cancellationToken).ConfigureAwait(false);
        if (listCode != 0 || string.IsNullOrWhiteSpace(listOut))
            return outdated.Values.ToList();

        var allInstalled = ParseScoopListOutput(listOut);
        var result = new List<PackageToolUpdate>();
        foreach (var (name, ver) in allInstalled)
        {
            if (outdated.TryGetValue(name, out var up))
            {
                result.Add(up);
            }
            else
            {
                result.Add(new PackageToolUpdate(PackageRunnerType.Scoop, name, ver, ver));
            }
        }
        return result;
    }

    public static Dictionary<string, PackageToolUpdate> ParseScoopStatusOutput(string stdout)
    {
        var map = new Dictionary<string, PackageToolUpdate>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(stdout)) return map;

        var lines = stdout.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        bool dataStarted = false;
        foreach (var line in lines)
        {
            if (line.StartsWith("---", StringComparison.Ordinal))
            {
                dataStarted = true;
                continue;
            }
            if (!dataStarted) continue;

            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 3)
            {
                var name = parts[0];
                var cur = parts[1];
                var latest = parts[2];
                map[name] = new PackageToolUpdate(PackageRunnerType.Scoop, name, cur, latest);
            }
        }
        return map;
    }

    public static List<(string Name, string Version)> ParseScoopListOutput(string stdout)
    {
        var list = new List<(string, string)>();
        if (string.IsNullOrWhiteSpace(stdout)) return list;

        var lines = stdout.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        bool dataStarted = false;
        foreach (var line in lines)
        {
            if (line.StartsWith("---", StringComparison.Ordinal))
            {
                dataStarted = true;
                continue;
            }
            if (!dataStarted) continue;

            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                list.Add((parts[0], parts[1]));
            }
        }
        return list;
    }

    public async Task<IReadOnlyList<PackageToolUpdate>> ScanChocolateyPackagesAsync(CancellationToken cancellationToken = default)
    {
        var probe = _runner.ProbeRunner(PackageRunnerType.Chocolatey);
        if (!probe.IsAvailable || probe.ExecutablePath is null)
            return Array.Empty<PackageToolUpdate>();

        var (_, outdatedOut, _) = await RunProcessAsync(probe.ExecutablePath, "outdated --limit-output", cancellationToken).ConfigureAwait(false);
        var outdated = ParseChocolateyOutdatedOutput(outdatedOut);

        var (listCode, listOut, _) = await RunProcessAsync(probe.ExecutablePath, "list --local-only --limit-output", cancellationToken).ConfigureAwait(false);
        if (listCode != 0 || string.IsNullOrWhiteSpace(listOut))
            return outdated.Values.ToList();

        var allInstalled = ParseChocolateyListOutput(listOut);
        var result = new List<PackageToolUpdate>();
        foreach (var (name, ver) in allInstalled)
        {
            if (outdated.TryGetValue(name, out var up))
            {
                result.Add(up);
            }
            else
            {
                result.Add(new PackageToolUpdate(PackageRunnerType.Chocolatey, name, ver, ver));
            }
        }
        return result;
    }

    public static Dictionary<string, PackageToolUpdate> ParseChocolateyOutdatedOutput(string stdout)
    {
        var map = new Dictionary<string, PackageToolUpdate>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(stdout)) return map;

        var lines = stdout.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var line in lines)
        {
            var parts = line.Split('|', StringSplitOptions.TrimEntries);
            if (parts.Length >= 3)
            {
                var name = parts[0];
                var cur = parts[1];
                var latest = parts[2];
                map[name] = new PackageToolUpdate(PackageRunnerType.Chocolatey, name, cur, latest);
            }
        }
        return map;
    }

    public static List<(string Name, string Version)> ParseChocolateyListOutput(string stdout)
    {
        var list = new List<(string, string)>();
        if (string.IsNullOrWhiteSpace(stdout)) return list;

        var lines = stdout.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var line in lines)
        {
            var parts = line.Split('|', StringSplitOptions.TrimEntries);
            if (parts.Length >= 2)
            {
                list.Add((parts[0], parts[1]));
            }
        }
        return list;
    }

    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunProcessAsync(
        string fileName,
        string arguments,
        CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process is null)
            return (-1, string.Empty, "Failed to start process");

        using var registration = cancellationToken.Register(() =>
        {
            try { process.Kill(entireProcessTree: true); } catch { }
        });

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await Task.WhenAll(stdoutTask, stderrTask).ConfigureAwait(false);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        return (process.ExitCode, await stdoutTask.ConfigureAwait(false), await stderrTask.ConfigureAwait(false));
    }
}
