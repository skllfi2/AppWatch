using AppWatch.Core.Abstractions;
using AppWatch.Core.Domain.Models;

namespace AppWatch.Infrastructure.Sources;

public sealed class WingetUpdateSourceProvider : IUpdateSourceProvider
{
    public string SourceId => "winget";
    public string DisplayName => "Windows Package Manager (WinGet)";

    public bool CanHandle(TrackedApp app)
    {
        return app.UpdateKind == UpdateKind.WingetPackage || !string.IsNullOrWhiteSpace(app.WingetPackageId);
    }

    public Task<UpdateCheckResult> CheckUpdateAsync(TrackedApp app, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new UpdateCheckResult(
            app,
            UpdateStatus.UpToDate,
            Detail: "winget packages are updated via scheduled upgrade-all"));
    }
}
