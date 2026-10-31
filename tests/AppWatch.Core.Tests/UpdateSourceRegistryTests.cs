using AppWatch.Core.Abstractions;
using AppWatch.Core.Domain;
using AppWatch.Core.Domain.Models;

namespace AppWatch.Core.Tests;

public class UpdateSourceRegistryTests
{
    private sealed class DummySourceProvider(string id, string name, Func<TrackedApp, bool> canHandle) : IUpdateSourceProvider
    {
        public string SourceId => id;
        public string DisplayName => name;
        public bool CanHandle(TrackedApp app) => canHandle(app);
        public Task<UpdateCheckResult> CheckUpdateAsync(TrackedApp app, CancellationToken cancellationToken = default) =>
            Task.FromResult(new UpdateCheckResult(app, UpdateStatus.UpToDate, Detail: $"handled by {id}"));
    }

    [Fact]
    public void Registry_ResolvesCorrectProvider()
    {
        var github = new DummySourceProvider("github", "GitHub", a => !string.IsNullOrEmpty(a.RepoOwner));
        var winget = new DummySourceProvider("winget", "WinGet", a => a.UpdateKind == UpdateKind.WingetPackage);
        var external = new DummySourceProvider("external", "External", a => a.UpdateKind == UpdateKind.External);

        var registry = new UpdateSourceRegistry([github, winget, external]);

        var ghApp = new TrackedApp { Name = "ripgrep", RepoOwner = "BurntSushi", RepoName = "ripgrep" };
        var wgApp = new TrackedApp { Name = "VSCode", RepoOwner = "", RepoName = "", UpdateKind = UpdateKind.WingetPackage, WingetPackageId = "Microsoft.VisualStudioCode" };
        var extApp = new TrackedApp { Name = "Steam", RepoOwner = "", RepoName = "", UpdateKind = UpdateKind.External };

        Assert.Equal("github", registry.Resolve(ghApp)?.SourceId);
        Assert.Equal("winget", registry.Resolve(wgApp)?.SourceId);
        Assert.Equal("external", registry.Resolve(extApp)?.SourceId);
    }

    [Fact]
    public void Registry_AllowsDynamicCustomSourceRegistration()
    {
        var registry = new UpdateSourceRegistry();

        var customScoop = new DummySourceProvider("scoop", "Scoop Package Manager", a => a.Name.StartsWith("scoop:"));
        registry.Register(customScoop);

        var scoopApp = new TrackedApp { Name = "scoop:neovim", RepoOwner = "", RepoName = "" };
        var resolved = registry.Resolve(scoopApp);

        Assert.NotNull(resolved);
        Assert.Equal("scoop", resolved.SourceId);
    }
}
