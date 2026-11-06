using AppWatch.Core.Abstractions;
using AppWatch.Core.Domain.Models;

namespace AppWatch.Core.Domain;

/// <summary>
/// Universal contract for release providers (GitHub, GitVerse, GitLab, Gitea).
/// Abstracts hosting specifics into a common query and asset downloading mechanism.
/// </summary>
public interface IGitReleaseClient
{
    /// <summary>Identifies the provider (e.g. "GitHub", "GitVerse", "GitLab").</summary>
    string ProviderName { get; }

    /// <summary>
    /// Checks whether this client supports the specified repository or host URL.
    /// </summary>
    bool CanHandle(string? repositoryUrlOrHost);

    /// <summary>
    /// Fetches available releases for the given repository.
    /// </summary>
    Task<IReadOnlyList<ReleaseInfo>> GetReleasesAsync(
        string owner,
        string repo,
        CancellationToken cancellationToken = default,
        string? etag = null);

    /// <summary>
    /// Downloads an asset from the release host to destination path.
    /// </summary>
    Task DownloadAssetAsync(
        string downloadUrl,
        string destinationPath,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);
}
