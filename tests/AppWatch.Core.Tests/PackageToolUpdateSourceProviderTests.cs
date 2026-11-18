using System.Threading.Tasks;
using AppWatch.Core.Domain.Models;
using AppWatch.Infrastructure.Sources;
using Xunit;

namespace AppWatch.Core.Tests;

public class PackageToolUpdateSourceProviderTests
{
    [Fact]
    public void CanHandle_WhenPackageToolManagerSet_ReturnsTrue()
    {
        var provider = new PackageToolUpdateSourceProvider();
        var app = new TrackedApp
        {
            Name = "vpk",
            RepoOwner = "",
            RepoName = "",
            PackageToolManager = "DotnetTool"
        };

        Assert.True(provider.CanHandle(app));
    }

    [Fact]
    public void CanHandle_WhenPackageToolManagerNull_ReturnsFalse()
    {
        var provider = new PackageToolUpdateSourceProvider();
        var app = new TrackedApp
        {
            Name = "bat",
            RepoOwner = "sharkdp",
            RepoName = "bat"
        };

        Assert.False(provider.CanHandle(app));
    }

    [Fact]
    public async Task CheckUpdateAsync_WhenUnknownManager_ReturnsError()
    {
        var provider = new PackageToolUpdateSourceProvider();
        var app = new TrackedApp
        {
            Name = "tool",
            RepoOwner = "",
            RepoName = "",
            PackageToolManager = "InvalidManager123"
        };

        var result = await provider.CheckUpdateAsync(app, TestContext.Current.CancellationToken);
        Assert.Equal(UpdateStatus.Error, result.Status);
    }
}
