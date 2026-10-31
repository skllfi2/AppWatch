using System.Collections.Generic;
using AppWatch.Core.Domain;
using Xunit;

namespace AppWatch.Core.Tests;

public class UpdateErrorHintsTests
{
    [Theory]
    [InlineData("Exit code 0x80070005: Access is denied", "HintElevationRequired")]
    [InlineData("0x800702e4: The requested operation requires elevation", "HintElevationRequired")]
    [InlineData("Installation failed: elevation required", "HintElevationRequired")]
    [InlineData("Exit code 0x80070020: The process cannot access the file because it is being used by another process", "HintFileLocked")]
    [InlineData("Sharing violation occurred while writing to executable", "HintFileLocked")]
    [InlineData("0x80070070: There is not enough space on the disk", "HintDiskFull")]
    [InlineData("Disk full", "HintDiskFull")]
    [InlineData("8006: installer failed with code 1", "HintWingetInstallFailed")]
    [InlineData("0x80072ee2: Connection timed out", "HintNetworkTimeout")]
    [InlineData("API rate limit exceeded for IP", "HintRateLimited")]
    [InlineData("SHA-256 mismatch: expected abc, got def", "HintDigestMismatch")]
    [InlineData("Random unknown error with no keyword", null)]
    [InlineData(null, null)]
    [InlineData("", null)]
    public void HintKey_MapsKnownErrorsAccurately(string? detail, string? expectedKey)
    {
        var key = UpdateErrorHints.HintKey(detail);
        Assert.Equal(expectedKey, key);
    }

    [Fact]
    public void WithHint_AppendsLocalizedExplanation_WhenHintExists()
    {
        var loc = new Dictionary<string, string>
        {
            ["HintElevationRequired"] = "Please run as administrator."
        };

        var result = UpdateErrorHints.WithHint("Error 0x80070005: Access is denied", k => loc[k]);
        Assert.Contains("Please run as administrator.", result);
        Assert.Contains("Access is denied", result);
    }

    [Fact]
    public void WithHint_ReturnsOriginal_WhenNoHintMatches()
    {
        var result = UpdateErrorHints.WithHint("Generic error", _ => "Hint");
        Assert.Equal("Generic error", result);
    }
}
