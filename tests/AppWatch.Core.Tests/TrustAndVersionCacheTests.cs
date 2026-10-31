using AppWatch.Core.Domain;

namespace AppWatch.Core.Tests;

public class InstallerTrustPolicyTests
{
    [Theory]
    [InlineData(InstallerTrustLevel.TrustedSignature, false, true)]
    [InlineData(InstallerTrustLevel.TrustedSignature, true, true)]
    [InlineData(InstallerTrustLevel.Unsigned, false, false)]
    [InlineData(InstallerTrustLevel.Unsigned, true, true)]
    [InlineData(InstallerTrustLevel.InvalidSignature, false, false)]
    [InlineData(InstallerTrustLevel.InvalidSignature, true, false)] // broken signature NEVER runs
    [InlineData(InstallerTrustLevel.UnsupportedFile, false, false)]
    [InlineData(InstallerTrustLevel.UnsupportedFile, true, true)]
    public void CanRun_GatesByLevelAndConsent(InstallerTrustLevel level, bool allowUnsigned, bool expected)
        => Assert.Equal(expected, InstallerTrustPolicy.CanRun(level, allowUnsigned));

    [Fact]
    public void RefusalReason_MentionsSignatureOrConsent()
    {
        Assert.Contains("not signed", InstallerTrustPolicy.RefusalReason(InstallerTrustLevel.Unsigned));
        Assert.Contains("invalid", InstallerTrustPolicy.RefusalReason(InstallerTrustLevel.InvalidSignature));
    }
}

public class VersionArchiveTests
{
    private static readonly DateTime Base = new(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc);

    private static VersionArchiveEntry E(string label, int minutesAgo) =>
        new($"C:\\v\\{label}", label, Base.AddMinutes(-minutesAgo));

    [Fact]
    public void NewestFirst_OrdersByWriteTimeDescending()
    {
        var ordered = VersionArchive.NewestFirst(
            [E("old", 300), E("newest", 10), E("middle", 60)]);

        Assert.Equal(["newest", "middle", "old"], ordered.Select(e => e.Label).ToArray());
    }

    [Fact]
    public void Prune_KeepsNewestN()
    {
        var entries = new[]
        {
            E("v1", 300), E("v2", 200), E("v3", 100), E("v4", 5),
        };

        var prune = VersionArchive.PickPruneTargets(entries, keep: 2);

        Assert.Equal(["v2", "v1"], prune.Select(e => e.Label).ToArray()); // oldest deleted
    }

    [Fact]
    public void Prune_WhenFewerThanKeep_DeletesNothing()
    {
        var entries = new[] { E("v1", 60), E("v2", 10) };

        Assert.Empty(VersionArchive.PickPruneTargets(entries, keep: 3));
    }

    [Fact]
    public void Prune_ZeroKeep_DeletesAll()
    {
        var entries = new[] { E("v1", 60), E("v2", 10) };

        Assert.Equal(2, VersionArchive.PickPruneTargets(entries, keep: 0).Count);
    }
}
