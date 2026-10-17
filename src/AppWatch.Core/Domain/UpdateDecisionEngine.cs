using AppWatch.Core.Domain.AssetSelection;
using AppWatch.Core.Domain.Models;
using AppWatch.Core.Domain.VersionDetection;

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

        // A1: user explicitly skipped this version — treat as UpToDate until a newer one appears
        if (app.SkippedVersion is { } skipped
            && string.Equals(skipped, newest.TagName, StringComparison.OrdinalIgnoreCase))
            return new(app, UpdateStatus.UpToDate, Detail: $"version {skipped} skipped by user");

        if (!VersionParser.IsNewer(app.CurrentVersion, newest.TagName))
            return new(app, UpdateStatus.UpToDate);

        var strategy = DetermineStrategy(app, newest);
        var priority = GetAssetPriority(app, strategy);

        var candidates = newest.Assets
            .Select(a => new GithubAssetCandidate(a.Name, a.Size))
            .Where(c => AssetSelector.ArchRank(c.Name, arch) > 0)
            .ToList();

        var chosen = AssetSelector.Choose(candidates, arch, priority);
        if (chosen is null)
            return new(app, UpdateStatus.NoMatchingAsset, newest, Detail: newest.TagName);

        var asset = newest.Assets.First(a =>
            string.Equals(a.Name, chosen.Name, StringComparison.OrdinalIgnoreCase));
        
        return new(app, UpdateStatus.UpdateAvailable, newest, asset, Detail: null, Strategy: strategy);
    }

    public static UpdateStrategy DetermineStrategy(TrackedApp app, ReleaseInfo release)
    {
        // If user specified a strategy, use it
        if (app.Strategy != UpdateStrategy.Auto)
            return app.Strategy;

        // Auto-detect based on asset types and app configuration
        var assets = release.Assets;
        var hasMsi = assets.Any(a => a.Name.EndsWith(".msi", StringComparison.OrdinalIgnoreCase));
        var hasExe = assets.Any(a => a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && !a.Name.Contains("portable", StringComparison.OrdinalIgnoreCase));
        var hasZip = assets.Any(a => a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) || a.Name.EndsWith(".7z", StringComparison.OrdinalIgnoreCase));
        
        // If app has winget package ID, prefer winget
        if (!string.IsNullOrEmpty(app.WingetPackageId))
            return UpdateStrategy.Winget;

        // Single-file portable releases (e.g. optimizerDuck bare .exe) replace the
        // install directory the same way archives do — no extraction, health-check exe.
        if (app.UpdateKind == UpdateKind.PortableExe)
            return UpdateStrategy.PortableReplace;

        // If app has built-in updater (has UpdateKind != PortableZip/PortableExe and has UpdateUrl), use built-in
        if (app.UpdateKind is not (UpdateKind.PortableZip or UpdateKind.PortableExe))
            return UpdateStrategy.BuiltInUpdater;

        // If MSI available, use silent installer
        if (hasMsi)
            return UpdateStrategy.SilentInstaller;

        // If EXE installer available, use silent installer
        if (hasExe)
            return UpdateStrategy.SilentInstaller;

        // If portable archive available, use portable replace
        if (hasZip)
            return UpdateStrategy.PortableReplace;

        // Default to manual
        return UpdateStrategy.Manual;
    }

    private static IReadOnlyList<string> GetAssetPriority(TrackedApp app, UpdateStrategy strategy)
    {
        return strategy switch
        {
            UpdateStrategy.Winget => [],
            UpdateStrategy.BuiltInUpdater => app.AssetExtensionPriority ?? [".exe", ".msi"],
            UpdateStrategy.SilentInstaller => [".msi", ".exe"],
            UpdateStrategy.PortableReplace => app.UpdateKind == UpdateKind.PortableExe
                ? [".exe", ".zip"]
                : [".zip", ".7z", ".tar.gz"],
            UpdateStrategy.ExternalLauncher => [".exe", ".msi"],
            _ => app.AssetExtensionPriority ?? (strategy == UpdateStrategy.PortableReplace ? [".zip", ".exe"] : [".msi", ".exe"])
        };
    }

    private static ParsedVersion? TryParse(string tag) =>
        VersionParser.TryParse(tag, out var v) ? v : null;
}
