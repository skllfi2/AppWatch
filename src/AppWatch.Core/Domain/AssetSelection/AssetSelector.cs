// Ported from Komi Store (https://github.com/komi-store/komi-store), Apache-2.0. See NOTICE.
using System.Text.RegularExpressions;
using AppWatch.Core.Domain.Models;

namespace AppWatch.Core.Domain.AssetSelection;

public static class AssetSelector
{
    private const int ArchExact = 3;
    private const int ArchUniversal = 2;
    private const int ArchFallback = 1;
    private const int ArchIncompatible = 0;

    private const int FlavorStable = 3;
    private const int FlavorPrerelease = 2;
    private const int FlavorUnstable = 1;
    private const int FlavorDebug = 0;

    private static readonly Regex DebugRegex = Boundary("debug");
    private static readonly Regex UnstableRegex = Boundary("nightly", "canary", "snapshot");
    private static readonly Regex PreReleaseRegex =
        new("""(^|[^a-z0-9])(alpha|beta|rc|preview|pre)[-_.]?\d*([^a-z0-9]|$)""");

    public static GithubAssetCandidate? Choose(
        IReadOnlyList<GithubAssetCandidate> assets,
        SystemArchitecture deviceArch,
        IReadOnlyList<string> extensionPriority)
    {
        if (assets.Count == 0) return null;
        return assets
            .OrderByDescending(a => ExtensionRank(a.Name, extensionPriority))
            .ThenByDescending(a => FlavorRank(a.Name))
            .ThenByDescending(a => ArchRank(a.Name, deviceArch))
            .ThenByDescending(a => a.Size)
            .ThenBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    public static int ArchRank(string assetName, SystemArchitecture deviceArch) =>
        AssetArchitectureMatcher.MatchArchitecture(assetName) switch
        {
            AssetArchitectureMatcher.UniversalMatch => ArchUniversal,
            AssetArchitectureMatcher.ForeignMatch => ArchIncompatible,
            AssetArchitectureMatcher.KnownMatch known =>
                known.Arch == deviceArch ? ArchExact
                : IsFallbackCompatible(known.Arch, deviceArch) ? ArchFallback
                : ArchIncompatible,
            _ => ArchIncompatible,
        };

    public static int FlavorRank(string assetName)
    {
        var name = assetName.ToLowerInvariant();
        return DebugRegex.IsMatch(name) ? FlavorDebug
            : UnstableRegex.IsMatch(name) ? FlavorUnstable
            : PreReleaseRegex.IsMatch(name) ? FlavorPrerelease
            : FlavorStable;
    }

    private static bool IsFallbackCompatible(SystemArchitecture assetArch, SystemArchitecture deviceArch) =>
        deviceArch switch
        {
            SystemArchitecture.Aarch64 => assetArch == SystemArchitecture.Arm,
            SystemArchitecture.X86_64 => assetArch == SystemArchitecture.X86,
            _ => false,
        };

    private static int ExtensionRank(string assetName, IReadOnlyList<string> extensionPriority)
    {
        if (extensionPriority.Count == 0) return 0;
        var name = assetName.ToLowerInvariant();
        for (var i = 0; i < extensionPriority.Count; i++)
        {
            if (name.EndsWith(extensionPriority[i], StringComparison.OrdinalIgnoreCase))
                return extensionPriority.Count - i;
        }
        return -1;
    }

    private static Regex Boundary(params string[] tokens) =>
        new($"""(^|[^a-z0-9])({string.Join("|", tokens)})([^a-z0-9]|$)""");
}
