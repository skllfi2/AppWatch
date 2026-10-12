using AppWatch.Core.Domain;
using Xunit;

namespace AppWatch.Core.Tests;

public class VersionParserTests
{
    [Theory]
    [InlineData("1.10.2", 1, 10, 2, 0, null)]
    [InlineData("v0.3.0", 0, 3, 0, 0, null)]
    [InlineData("V2.71.0", 2, 71, 0, 0, null)]
    [InlineData("0.2.0-rc.2", 0, 2, 0, 0, "rc.2")]
    [InlineData("1.0.0-beta.1", 1, 0, 0, 0, "beta.1")]
    [InlineData("1.19.3.3219", 1, 19, 3, 3219, null)]
    [InlineData("26.10.0.1796", 26, 10, 0, 1796, null)]
    [InlineData("1.29.380", 1, 29, 380, 0, null)]
    public void TryParse_ParsesVersions(string raw, long major, long minor, long patch, long revision, string? pre)
    {
        var ok = VersionParser.TryParse(raw, out var version);

        Assert.True(ok);
        Assert.NotNull(version);
        Assert.Equal(major, version.Major);
        Assert.Equal(minor, version.Minor);
        Assert.Equal(patch, version.Patch);
        Assert.Equal(revision, version.Revision);
        Assert.Equal(pre, version.PreRelease);
    }

    [Theory]
    [InlineData("0.4.25+1")]
    public void TryParse_StripsBuildMetadata(string raw)
    {
        var ok = VersionParser.TryParse(raw, out var version);

        Assert.True(ok);
        Assert.Equal(new ParsedVersion(0, 4, 25, 0, null), version);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("1.2.3.4.5")]
    public void TryParse_RejectsGarbage(string raw)
    {
        Assert.False(VersionParser.TryParse(raw, out _));
    }

    [Fact]
    public void IsNewer_UnknownCurrent_IsTrue()
    {
        Assert.True(VersionParser.IsNewer(null, "0.1.0"));
    }

    [Theory]
    [InlineData("1.10.2", "1.10.3", true)]
    [InlineData("1.10.3", "1.10.2", false)]
    [InlineData("2.70.0", "2.71.0", true)]
    [InlineData("1.0.0", "1.0.1-beta", true)]
    [InlineData("1.0.0-beta", "1.0.0", true)]
    [InlineData("1.0.0-rc.1", "1.0.0-rc.2", true)]
    [InlineData("1.0.0-beta", "1.0.0-alpha", false)]
    [InlineData("0.2.0-rc.2", "0.3.0", true)]
    public void IsNewer_ComparesSemver(string current, string candidate, bool expected)
    {
        Assert.Equal(expected, VersionParser.IsNewer(current, candidate));
    }

    [Fact]
    public void IsNewer_FourPartVersions()
    {
        Assert.True(VersionParser.IsNewer("1.19.3.3218", "1.19.3.3219"));
        Assert.False(VersionParser.IsNewer("1.19.3.3219", "1.19.3.3219"));
        Assert.False(VersionParser.IsNewer("2.53.4.17896", "1.19.3.3219"));
    }
}
