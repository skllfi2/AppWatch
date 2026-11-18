using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AppWatch.Core.Abstractions;
using AppWatch.Core.Domain;
using AppWatch.Core.Domain.Models;

namespace AppWatch.Infrastructure.Sources;

/// <summary>
/// Update source provider for applications tracked via RSS/Atom feeds or scraped website release pages.
/// </summary>
public sealed class FeedUpdateSourceProvider : IUpdateSourceProvider
{
    private readonly FeedWebReleaseClient _client;

    public string SourceId => "feed_web";
    public string DisplayName => "Web / RSS Feed";

    public FeedUpdateSourceProvider(FeedWebReleaseClient? client = null)
    {
        _client = client ?? new FeedWebReleaseClient();
    }

    public bool CanHandle(TrackedApp app)
    {
        return app.UpdateKind == UpdateKind.FeedOrWeb ||
               app.SourceOrigin == AppSourceOrigin.FeedOrWeb ||
               !string.IsNullOrWhiteSpace(app.FeedOrWebUrl);
    }

    public async Task<UpdateCheckResult> CheckUpdateAsync(TrackedApp app, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(app.FeedOrWebUrl))
        {
            return new UpdateCheckResult(app, UpdateStatus.Error, Detail: "Feed/Web URL is missing");
        }

        try
        {
            var release = await _client.GetLatestReleaseAsync(
                app.FeedOrWebUrl,
                app.WebVersionRegex,
                app.DownloadUrlTemplate,
                cancellationToken).ConfigureAwait(false);

            if (release is null)
            {
                return new UpdateCheckResult(app, UpdateStatus.Error, Detail: "Unable to find version on target page/feed");
            }

            var chosenAsset = release.Assets.FirstOrDefault();
            var current = app.CurrentVersion;
            var available = release.TagName;

            if (string.IsNullOrWhiteSpace(current))
            {
                return new UpdateCheckResult(app, UpdateStatus.UpdateAvailable, release, chosenAsset, "Initial version detected");
            }

            if (string.Equals(current, available, StringComparison.OrdinalIgnoreCase))
            {
                return new UpdateCheckResult(app, UpdateStatus.UpToDate, release, chosenAsset, "Up to date");
            }

            return new UpdateCheckResult(app, UpdateStatus.UpdateAvailable, release, chosenAsset, $"New version {available} available");
        }
        catch (Exception ex)
        {
            return new UpdateCheckResult(app, UpdateStatus.Error, Detail: ex.Message);
        }
    }
}
