using AppWatch.Core.Domain.AssetSelection;
using AppWatch.Core.Domain.Models;

namespace AppWatch.Core.Domain;

public static class UpdateDecisionEngine
{
    public static UpdateCheckResult Evaluate(
        TrackedApp app, IReadOnlyList<ReleaseInfo> releases, SystemArchitecture arch)
    {
        if (!app.Enabled)
            return new(app, UpdateStatus.Disabled);

        var ordered = releases
            .Where(r => !app.IgnorePreReleases || !r.IsPreRelease)
            .Select(r => (Release: r, Parsed: TryParse(r.TagName)))
            .OrderByDescending(x => x.Parsed)
            .ThenByDescending(x => x.Release.PublishedAt)
            .ToList();

        if (app.PinnedVersion is not null)
        {
            var latestName = ordered.Count > 0 ? ordered[0].Release.TagName : "none";
            return new(app, UpdateStatus.Pinned,
                Detail: $"pinned to {app.PinnedVersion} (latest available: {latestName})");
        }

        if (ordered.Count == 0)
            return new(app, UpdateStatus.UpToDate, Detail: "no matching releases");

        var newest = ordered[0].Release;

        if (!VersionParser.IsNewer(app.CurrentVersion, newest.TagName))
            return new(app, UpdateStatus.UpToDate);

        var priority = app.AssetExtensionPriority
            ?? (app.UpdateKind == UpdateKind.PortableZip
                ? (IReadOnlyList<string>)[ ".zip", ".exe" ]
                : [ ".msi", ".exe" ]);

        var candidates = newest.Assets
            .Select(a => new GithubAssetCandidate(a.Name, a.Size))
            .Where(c => AssetSelector.ArchRank(c.Name, arch) > 0)
            .ToList();

        var chosen = AssetSelector.Choose(candidates, arch, priority);
        if (chosen is null)
            return new(app, UpdateStatus.NoMatchingAsset, newest, Detail: newest.TagName);

        var asset = newest.Assets.First(a =>
            string.Equals(a.Name, chosen.Name, StringComparison.OrdinalIgnoreCase));
        return new(app, UpdateStatus.UpdateAvailable, newest, asset);
    }

    private static ParsedVersion? TryParse(string tag) =>
        VersionParser.TryParse(tag, out var v) ? v : null;
}
