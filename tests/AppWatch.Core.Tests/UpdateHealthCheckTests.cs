using AppWatch.Core.Abstractions;
using AppWatch.Core.Domain;

namespace AppWatch.Core.Tests;

/// <summary>IProcessService fake: returns queued alive-samples, repeating the last one.</summary>
public sealed class FakeProcessService : IProcessService
{
    private readonly Queue<bool> _samples = new();
    private bool _last;

    public FakeProcessService(params bool[] samples)
    {
        foreach (var s in samples) _samples.Enqueue(s);
    }

    public int KillCount { get; private set; }

    public bool IsRunning(string processName)
    {
        if (_samples.Count > 0) _last = _samples.Dequeue();
        return _last;
    }

    public bool Kill(string processName)
    {
        KillCount++;
        return true;
    }
}

public sealed class RecordingLauncher : IAppLauncher
{
    public List<string> Launched { get; } = [];

    public void Launch(string executablePath) => Launched.Add(executablePath);
}

public class UpdateHealthCheckTests
{
    private static Task NoDelay(int ms, CancellationToken ct) => Task.CompletedTask;

    [Fact]
    public async Task Verify_StaysAlive_ReturnsHealthy()
    {
        var process = new FakeProcessService(true, true, true, true, true, true);
        var launcher = new RecordingLauncher();

        var outcome = await UpdateHealthCheck.VerifyStartupAsync(
            @"C:\apps\MyApp\app.exe", "app", process, launcher,
            windowSeconds: 2, delay: NoDelay, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(outcome.Healthy);
        Assert.Null(outcome.Reason);
        Assert.Single(launcher.Launched);
    }

    [Fact]
    public async Task Verify_NeverStarts_ReturnsUnhealthyWithReason()
    {
        var process = new FakeProcessService(false, false, false, false, false, false);

        var outcome = await UpdateHealthCheck.VerifyStartupAsync(
            "exe", "app", process, new RecordingLauncher(),
            windowSeconds: 2, delay: NoDelay, cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(outcome.Healthy);
        Assert.Contains("did not start", outcome.Reason);
    }

    [Fact]
    public async Task Verify_StartsThenExits_ReturnsUnhealthyWithReason()
    {
        var process = new FakeProcessService(true, false, false, false);

        var outcome = await UpdateHealthCheck.VerifyStartupAsync(
            "exe", "app", process, new RecordingLauncher(),
            windowSeconds: 2, delay: NoDelay, cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(outcome.Healthy);
        Assert.Contains("exited within", outcome.Reason);
    }

    [Fact]
    public async Task Verify_NoExecutable_TriviallyHealthy()
    {
        var outcome = await UpdateHealthCheck.VerifyStartupAsync(
            null, "app", new FakeProcessService(false), new RecordingLauncher(),
            windowSeconds: 2, delay: NoDelay, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(outcome.Healthy);
    }

    [Fact]
    public async Task Verify_NoProcessName_TriviallyHealthy()
    {
        var outcome = await UpdateHealthCheck.VerifyStartupAsync(
            "exe", null, new FakeProcessService(false), new RecordingLauncher(),
            windowSeconds: 2, delay: NoDelay, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(outcome.Healthy);
    }
}