using System.Text.RegularExpressions;
using AppWatch.Core.Domain.Models;

namespace AppWatch.Core.Domain.AssetSelection;

public static class AssetArchitectureMatcher
{
    public abstract record Match;
    public sealed record UniversalMatch : Match;
    public sealed record ForeignMatch : Match;
    public sealed record KnownMatch(SystemArchitecture Arch) : Match;

    private static readonly Regex UniversalRegex = Boundary("universal", "anycpu", "any");
    private static readonly Regex Arm64Regex = Boundary("arm64", "aarch64", "armv8", "win-arm64");
    private static readonly Regex ArmRegex = Boundary("arm", "armv7", "armhf");
    private static readonly Regex X64Regex = Boundary("x64", "x86_64", "amd64", "win64", "win-x64");
    private static readonly Regex X86Regex = Boundary("x86", "win32", "i386", "i686", "win-x86");

    public static Match MatchArchitecture(string assetName)
    {
        ArgumentNullException.ThrowIfNull(assetName);
        if (UniversalRegex.IsMatch(assetName)) return new UniversalMatch();
        if (Arm64Regex.IsMatch(assetName)) return new KnownMatch(SystemArchitecture.Aarch64);
        if (ArmRegex.IsMatch(assetName)) return new KnownMatch(SystemArchitecture.Arm);
        if (X64Regex.IsMatch(assetName)) return new KnownMatch(SystemArchitecture.X86_64);
        if (X86Regex.IsMatch(assetName)) return new KnownMatch(SystemArchitecture.X86);
        // P0 live-fix: GitHub convention — a name with NO arch marker (e.g.
        // "zapret-discord-youtube-1.10.3.zip") is universal, not foreign. Treating it
        // as ForeignMatch dropped every markerless asset in the engine filter.
        return new UniversalMatch();
    }

    private static Regex Boundary(params string[] tokens) =>
        new($"""(^|[^a-z0-9])({string.Join("|", tokens)})([^a-z0-9]|$)""", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
}
