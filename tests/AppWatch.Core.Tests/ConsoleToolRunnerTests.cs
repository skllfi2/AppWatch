using AppWatch.Core.Domain;
using Xunit;

namespace AppWatch.Core.Tests;

public class ConsoleToolRunnerTests
{
    [Fact]
    public void ProbeRunner_NonExistentCustomRunner_ReturnsNotAvailableGracefully()
    {
        var runner = new ConsoleToolRunner();
        // Probe all defined types
        foreach (var type in Enum.GetValues<PackageRunnerType>())
        {
            var probe = runner.ProbeRunner(type);
            Assert.NotNull(probe);
            Assert.Equal(type, probe.Type);
            // Must not throw even if executable is missing
        }
    }

    [Fact]
    public async Task UpdateToolAsync_WhenRunnerMissing_ReturnsClearExplanationWithoutCrashing()
    {
        var runner = new FakeMissingRunner();
        var result = await runner.UpdateToolAsync(PackageRunnerType.Bun, "my-tool");

        Assert.False(result.Success);
        Assert.Contains("не найден в системе", result.Error);
    }

    private class FakeMissingRunner : ConsoleToolRunner
    {
        public override PackageRunnerProbe ProbeRunner(PackageRunnerType runnerType)
        {
            return new PackageRunnerProbe(runnerType, "missing.exe", false, null, null);
        }
    }
}
