namespace AppWatch.Core.Domain.Models;

public sealed record ReleaseAsset(string Name, string DownloadUrl, long Size, string? DigestSha256);

public sealed class ReleaseInfo
{
    public required string TagName { get; init; }
    public bool IsPreRelease { get; init; }
    public DateTimeOffset PublishedAt { get; init; }
    public IReadOnlyList<ReleaseAsset> Assets { get; init; } = [];
}

public enum UpdateStatus
{
    UpToDate,
    UpdateAvailable,
    Pinned,
    NoMatchingAsset,
    Disabled,
    Error,
}

public sealed record UpdateCheckResult(
    TrackedApp App,
    UpdateStatus Status,
    ReleaseInfo? NewRelease = null,
    ReleaseAsset? ChosenAsset = null,
    string? Detail = null);
