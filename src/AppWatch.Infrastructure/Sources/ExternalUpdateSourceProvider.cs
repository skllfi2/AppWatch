using AppWatch.Core.Abstractions;
using AppWatch.Core.Domain.Models;
using AppWatch.Core.Domain.VersionDetection;
using Serilog;

namespace AppWatch.Infrastructure.Sources;

public sealed class ExternalUpdateSourceProvider : IUpdateSourceProvider
{
    public string SourceId => "external";
    public string DisplayName => "External / System Managed";

    public bool CanHandle(TrackedApp app)
    {
        return app.UpdateKind == UpdateKind.External;
    }

    public async Task<UpdateCheckResult> CheckUpdateAsync(TrackedApp app, CancellationToken cancellationToken = default)
    {
        if (app.AutoDetectVersion)
        {
            var detected = await VersionDetector.DetectAsync(app, cancellationToken).ConfigureAwait(false);
            if (detected is not null && detected.Version != app.CurrentVersion)
            {
                Log.Information("version auto-detected for {App}: {Version} ({Source}), stored: {Stored}",
                    app.Name, detected.Version, detected.Source, app.CurrentVersion ?? "none");
                app.CurrentVersion = detected.Version;
            }
        }

        return new UpdateCheckResult(
            app,
            UpdateStatus.UpToDate,
            Detail: "managed by its own updater");
    }
}
