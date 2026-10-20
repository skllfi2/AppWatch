using AppWatch.Core.Domain;

namespace AppWatch.Core.Tests;

/// <summary>UPD-03: per-app update lock — concurrent operations on the same app are refused.</summary>
public class UpdateGateTests
{
    [Fact]
    public void TryAcquire_FirstCall_ReturnsReleaser()
    {
        var gate = new UpdateGate();
        using var claim = gate.TryAcquire("App");
        Assert.NotNull(claim);
    }

    [Fact]
    public void TryAcquire_SecondConcurrentCall_ReturnsNull()
    {
        var gate = new UpdateGate();
        using var first = gate.TryAcquire("App");
        Assert.Null(gate.TryAcquire("App"));
    }

    [Fact]
    public void TryAcquire_DifferentApps_BothSucceed()
    {
        var gate = new UpdateGate();
        using var a = gate.TryAcquire("AppA");
        using var b = gate.TryAcquire("AppB");
        Assert.NotNull(a);
        Assert.NotNull(b);
    }

    [Fact]
    public void TryAcquire_AfterRelease_CanReacquire()
    {
        var gate = new UpdateGate();
        var first = gate.TryAcquire("App");
        Assert.NotNull(first);
        first!.Dispose();
        // double dispose must be a no-op, not a double-release
        first.Dispose();

        using var second = gate.TryAcquire("App");
        Assert.NotNull(second);
    }

    [Fact]
    public void TryAcquire_CaseInsensitiveKey()
    {
        var gate = new UpdateGate();
        using var first = gate.TryAcquire("App");
        Assert.Null(gate.TryAcquire("app"));
    }

    [Fact]
    public void IsBusy_ReflectsClaimLifecycle()
    {
        var gate = new UpdateGate();
        Assert.False(gate.IsBusy("App"));
        var claim = gate.TryAcquire("App");
        Assert.True(gate.IsBusy("App"));
        Assert.True(gate.IsBusy("APP"));
        claim!.Dispose();
        Assert.False(gate.IsBusy("App"));
    }

    [Fact]
    public void TryAcquire_EmptyKey_Throws()
    {
        var gate = new UpdateGate();
        Assert.Throws<ArgumentException>(() => gate.TryAcquire(""));
    }
}
