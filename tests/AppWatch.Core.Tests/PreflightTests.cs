using AppWatch.Core.Domain;
using Xunit;

namespace AppWatch.Core.Tests;

public class PreflightTests
{
    // P5: Disk-space preflight — pure function, injectable for testing

    [Fact]
    public void CheckDiskSpace_SufficientSpace_ReturnsTrue()
    {
        var (ok, reason) = Preflight.CheckDiskSpace(100_000_000, 50_000_000);
        Assert.True(ok);
        Assert.Null(reason);
    }

    [Fact]
    public void CheckDiskSpace_ExactSpace_ReturnsTrue()
    {
        var (ok, reason) = Preflight.CheckDiskSpace(50_000_000, 50_000_000);
        Assert.True(ok);
        Assert.Null(reason);
    }

    [Fact]
    public void CheckDiskSpace_InsufficientSpace_ReturnsFalseWithReason()
    {
        var (ok, reason) = Preflight.CheckDiskSpace(10_000_000, 50_000_000);
        Assert.False(ok);
        Assert.Contains("Insufficient disk space", reason);
        Assert.Contains("10000000", reason); // raw free bytes
        Assert.Contains("50000000", reason); // raw required bytes
    }

    [Fact]
    public void CheckDiskSpace_ZeroRequired_ReturnsTrue()
    {
        var (ok, reason) = Preflight.CheckDiskSpace(0, 0);
        Assert.True(ok);
        Assert.Null(reason);
    }

    [Fact]
    public void CheckDiskSpace_NegativeRequired_TreatedAsZero()
    {
        var (ok, reason) = Preflight.CheckDiskSpace(100, -50);
        Assert.True(ok);
        Assert.Null(reason);
    }

    [Fact]
    public void CheckDiskSpace_PathBased_SucceedsOnRealVolume()
    {
        // Uses real DriveInfo — should pass on any machine with >1MB free on temp volume
        var temp = Path.GetTempPath();
        var (ok, reason) = Preflight.CheckDiskSpace(temp, 1_000_000);
        Assert.True(ok, reason ?? "expected sufficient space on temp volume");
    }

    [Fact]
    public void CheckDiskSpace_PathBased_InvalidPath_ReturnsFalse()
    {
        var (ok, reason) = Preflight.CheckDiskSpace("", 100);
        Assert.False(ok);
        Assert.Contains("Cannot determine volume root", reason);
    }

    [Fact]
    public void CheckDiskSpace_PathBased_NonExistentRoot_ReturnsFalse()
    {
        // Z: typically doesn't exist — DriveInfo.IsReady = false
        var (ok, reason) = Preflight.CheckDiskSpace(@"Z:\nonexistent", 100);
        Assert.False(ok);
        Assert.Contains("not ready", reason);
    }
}