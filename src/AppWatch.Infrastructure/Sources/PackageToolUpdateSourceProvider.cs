using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AppWatch.Core.Abstractions;
using AppWatch.Core.Domain;
using AppWatch.Core.Domain.Models;
using AppWatch.Infrastructure.System;
using Serilog;

namespace AppWatch.Infrastructure.Sources;

/// <summary>
/// Handles apps managed by global console package managers (npm, bun, dotnet tool, cargo, pipx).
/// </summary>
public sealed class PackageToolUpdateSourceProvider : IUpdateSourceProvider
{
    private readonly PackageToolScanner _scanner;
    private readonly ConsoleToolRunner _runner;

    public PackageToolUpdateSourceProvider(PackageToolScanner? scanner = null, ConsoleToolRunner? runner = null)
    {
        _scanner = scanner ?? new PackageToolScanner();
        _runner = runner ?? new ConsoleToolRunner();
    }

    public string SourceId => "packagetool";
    public string DisplayName => "Global Package Manager Tool";

    public bool CanHandle(TrackedApp app)
    {
        return !string.IsNullOrWhiteSpace(app.PackageToolManager);
    }

    public async Task<UpdateCheckResult> CheckUpdateAsync(TrackedApp app, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(app.PackageToolManager))
        {
            return new UpdateCheckResult(app, UpdateStatus.Error, Detail: "Package manager not configured");
        }

        if (!Enum.TryParse<PackageRunnerType>(app.PackageToolManager, true, out var runnerType))
        {
            return new UpdateCheckResult(app, UpdateStatus.Error, Detail: $"Unknown package manager: {app.PackageToolManager}");
        }

        var probe = _runner.ProbeRunner(runnerType);
        if (!probe.IsAvailable)
        {
            return new UpdateCheckResult(app, UpdateStatus.Error, Detail: $"{runnerType} is not installed or not in PATH");
        }

        try
        {
            var allTools = await _scanner.ScanAllAsync(cancellationToken).ConfigureAwait(false);
            var match = allTools.FirstOrDefault(t => 
                t.RunnerType == runnerType && 
                string.Equals(t.PackageName, app.ExecutableName ?? app.Name, StringComparison.OrdinalIgnoreCase));

            if (match is null)
            {
                return new UpdateCheckResult(app, UpdateStatus.UpToDate, Detail: "Installed globally");
            }

            if (match.HasUpdate)
            {
                var releaseInfo = new ReleaseInfo
                {
                    TagName = match.AvailableVersion,
                    Body = $"Global package update via {runnerType}"
                };

                return new UpdateCheckResult(
                    app,
                    UpdateStatus.UpdateAvailable,
                    NewRelease: releaseInfo,
                    Strategy: UpdateStrategy.ExternalLauncher,
                    Detail: $"{match.CurrentVersion} → {match.AvailableVersion}");
            }

            return new UpdateCheckResult(app, UpdateStatus.UpToDate, Detail: match.CurrentVersion);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to check package tool update for {App}", app.Name);
            return new UpdateCheckResult(app, UpdateStatus.Error, Detail: ex.Message);
        }
    }
}
