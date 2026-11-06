using System.Diagnostics;
using System.Runtime.InteropServices;
using AppWatch.Core.Domain.Models;

namespace AppWatch.Core.Domain;

public enum PackageRunnerType
{
    Npm,
    Bun,
    Cargo,
    Pipx,
    DotnetTool
}

public sealed record PackageRunnerProbe(
    PackageRunnerType Type,
    string ExecutableName,
    bool IsAvailable,
    string? ExecutablePath,
    string? Version
);

public sealed record ConsoleToolExecutionResult(
    bool Success,
    int ExitCode,
    string Output,
    string? Error
);

/// <summary>
/// Discovers and executes operations via CLI package managers (npm, bun, cargo, pipx, dotnet).
/// Gracefully handles absent runtimes by detecting them before execution and offering direct installation/fallback.
/// </summary>
public class ConsoleToolRunner
{
    public static readonly Dictionary<PackageRunnerType, (string Exe, string VersionArg, string UpdateCommandTemplate)> RunnerSpecs = new()
    {
        [PackageRunnerType.Npm] = ("npm.cmd", "--version", "update -g {0}"),
        [PackageRunnerType.Bun] = ("bun.exe", "--version", "update -g {0}"),
        [PackageRunnerType.Cargo] = ("cargo.exe", "--version", "install-update {0}"),
        [PackageRunnerType.Pipx] = ("pipx.exe", "--version", "upgrade {0}"),
        [PackageRunnerType.DotnetTool] = ("dotnet.exe", "tool --version", "tool update -g {0}")
    };

    /// <summary>
    /// Checks if a package manager runtime is installed and accessible in the system PATH.
    /// </summary>
    public virtual PackageRunnerProbe ProbeRunner(PackageRunnerType runnerType)
    {
        if (!RunnerSpecs.TryGetValue(runnerType, out var spec))
        {
            return new PackageRunnerProbe(runnerType, "unknown", false, null, null);
        }

        var path = FindOnPath(spec.Exe);
        if (path is null && RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && !spec.Exe.EndsWith(".exe") && !spec.Exe.EndsWith(".cmd"))
        {
            path = FindOnPath(spec.Exe + ".exe") ?? FindOnPath(spec.Exe + ".cmd");
        }

        if (path is null)
        {
            return new PackageRunnerProbe(runnerType, spec.Exe, false, null, null);
        }

        string? version = null;
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = path,
                Arguments = spec.VersionArg,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            if (proc is not null)
            {
                if (proc.WaitForExit(3000))
                {
                    version = proc.StandardOutput.ReadToEnd().Trim();
                }
                else
                {
                    proc.Kill();
                }
            }
        }
        catch
        {
            // Ignore probe errors safely
        }

        return new PackageRunnerProbe(runnerType, spec.Exe, true, path, version);
    }

    /// <summary>
    /// Executes a package tool update command (e.g. `npm update -g @foo/bar`).
    /// If the runner is missing, returns a clean failure result with an actionable message.
    /// </summary>
    public virtual async Task<ConsoleToolExecutionResult> UpdateToolAsync(
        PackageRunnerType runnerType,
        string packageName,
        IProgress<string>? logProgress = null,
        CancellationToken cancellationToken = default)
    {
        var probe = ProbeRunner(runnerType);
        if (!probe.IsAvailable || string.IsNullOrEmpty(probe.ExecutablePath))
        {
            var missingMsg = $"Пакетный менеджер '{probe.ExecutableName}' не найден в системе (%PATH%). Установите {runnerType} или переключите приложение на Portable/GitHub бинарник.";
            logProgress?.Report(missingMsg);
            return new ConsoleToolExecutionResult(false, -1, "", missingMsg);
        }

        var spec = RunnerSpecs[runnerType];
        var args = string.Format(spec.UpdateCommandTemplate, packageName);

        return await RunProcessAsync(probe.ExecutablePath, args, logProgress, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Resolves an executable on system PATH.
    /// </summary>
    public static string? FindOnPath(string executableName)
    {
        if (File.Exists(executableName)) return Path.GetFullPath(executableName);

        var pathVar = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathVar)) return null;

        var entries = pathVar.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var dir in entries)
        {
            try
            {
                var full = Path.Combine(dir, executableName);
                if (File.Exists(full)) return full;
            }
            catch
            {
                // skip malformed entries safely
            }
        }

        return null;
    }

    private static async Task<ConsoleToolExecutionResult> RunProcessAsync(
        string fileName,
        string arguments,
        IProgress<string>? logProgress,
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

        using var process = new Process { StartInfo = psi };
        var outputBuilder = new System.Text.StringBuilder();
        var errorBuilder = new System.Text.StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                outputBuilder.AppendLine(e.Data);
                logProgress?.Report(e.Data);
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                errorBuilder.AppendLine(e.Data);
                logProgress?.Report(e.Data);
            }
        };

        try
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

            return new ConsoleToolExecutionResult(
                Success: process.ExitCode == 0,
                ExitCode: process.ExitCode,
                Output: outputBuilder.ToString(),
                Error: errorBuilder.ToString()
            );
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            return new ConsoleToolExecutionResult(false, -1, outputBuilder.ToString(), "Operation cancelled.");
        }
        catch (Exception ex)
        {
            return new ConsoleToolExecutionResult(false, -1, outputBuilder.ToString(), ex.Message);
        }
    }
}
