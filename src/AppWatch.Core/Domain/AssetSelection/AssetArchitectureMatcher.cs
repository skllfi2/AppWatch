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
        var name = assetName.ToLowerInvariant();
        if (UniversalRegex.IsMatch(name)) return new UniversalMatch();
        if (Arm64Regex.IsMatch(name)) return new KnownMatch(SystemArchitecture.Aarch64);
        if (ArmRegex.IsMatch(name)) return new KnownMatch(SystemArchitecture.Arm);
        if (X64Regex.IsMatch(name)) return new KnownMatch(SystemArchitecture.X86_64);
        if (X86Regex.IsMatch(name)) return new KnownMatch(SystemArchitecture.X86);
        return new ForeignMatch();
    }

    private static Regex Boundary(params string[] tokens) =>
        new($"""(^|[^a-z0-9])({string.Join("|", tokens)})([^a-z0-9]|$)""");
}
