using System.Text.RegularExpressions;

namespace AppWatch.Core.Domain.AssetSelection;

/// <summary>
/// D2 (oct18): excludes release assets that can never be installed on Windows —
/// non-Windows package formats, macOS/Linux name markers, and release metadata junk
/// (electron-updater feeds, blockmaps, flatpak metainfo, SBOMs). Pure function,
/// hermetic. Extracted from UpdateDecisionEngine so the contract is directly testable.
/// </summary>
public static class PlatformAssetFilter
{
    // Non-Windows package formats named in RESEARCH.md D2 (.deb .rpm .apk .flatpak
    // .snap .AppImage .dmg .jar) + tarballs + metadata files observed in LIVE catalog
    // releases (rustdesk 1.5.0: .pkg.tar.zst, rustdesk.sbom.json; Vesktop 1.6.7:
    // latest*.yml, *.blockmap, dev.vencord.Vesktop.metainfo.xml — gh api 2026-10-19).
    private static readonly Regex NonWindowsExtensionRegex = new(
        @"\.(deb|rpm|apk|flatpak|snap|AppImage|dmg|pkg|jar|run|sh|bin|json|xml|yml|yaml|blockmap|tar\.gz|tar\.xz|tar\.zst|tar\.bz2)(\?.*)?$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Name markers: "Vesktop-1.6.7-universal-mac.zip" is a macOS build inside a .zip —
    // on x64 it used to WIN asset selection by size over the real win.zip (live bug).
    // Boundary tokens so "machine"/"emacs" don't trip the "mac" marker.
    private static readonly Regex NonWindowsNameRegex =
        new(@"(^|[^a-z0-9])(macos|osx|mac|linux)([^a-z0-9]|$)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Assets with no recognized non-Windows marker are assumed compatible.</summary>
    public static bool IsWindowsCompatible(string assetName) =>
        !NonWindowsExtensionRegex.IsMatch(assetName) && !NonWindowsNameRegex.IsMatch(assetName);
}
