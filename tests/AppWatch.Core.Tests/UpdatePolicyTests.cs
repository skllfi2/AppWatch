using AppWatch.Core.Domain;
using AppWatch.Core.Domain.Models;
using Xunit;

namespace AppWatch.Core.Tests;

public class UpdatePolicyTests
{
    private static AppConfig Config(Action<AppConfig>? configure = null)
    {
        var cfg = new AppConfig { AutoApplyUpdates = false };
        configure?.Invoke(cfg);
        return cfg;
    }

    private static TrackedApp App(Action<TrackedApp>? configure = null)
    {
        var app = new TrackedApp
        {
            Name = "Test",
            RepoOwner = "o",
            RepoName = "r",
            Enabled = true,
            LaunchAfterUpdate = true,
        };
        configure?.Invoke(app);
        return app;
    }

    // P1: default OFF — config flag false means never auto-apply
    [Fact]
    public void ShouldAutoApply_DefaultConfigAutoApplyFalse_ReturnsFalse()
    {
        var cfg = Config();
        var app = App();
        Assert.False(UpdatePolicy.ShouldAutoApply(cfg, app));
    }

    // P1: respects config flag — when true and app enabled + launch after, returns true
    [Fact]
    public void ShouldAutoApply_ConfigTrue_AppEnabled_LaunchAfterTrue_ReturnsTrue()
    {
        var cfg = Config(c => c.AutoApplyUpdates = true);
        var app = App();
        Assert.True(UpdatePolicy.ShouldAutoApply(cfg, app));
    }

    // P1: respects TrackedApp.Enabled — disabled app never auto-applies even if config says yes
    [Fact]
    public void ShouldAutoApply_AppDisabled_ReturnsFalse()
    {
        var cfg = Config(c => c.AutoApplyUpdates = true);
        var app = App(a => a.Enabled = false);
        Assert.False(UpdatePolicy.ShouldAutoApply(cfg, app));
    }

    // P1: respects TrackedApp.LaunchAfterUpdate — if false, no auto-apply (user chose manual launch)
    [Fact]
    public void ShouldAutoApply_LaunchAfterUpdateFalse_ReturnsFalse()
    {
        var cfg = Config(c => c.AutoApplyUpdates = true);
        var app = App(a => a.LaunchAfterUpdate = false);
        Assert.False(UpdatePolicy.ShouldAutoApply(cfg, app));
    }

    // P1: null checks — defensive, should throw
    [Fact]
    public void ShouldAutoApply_NullConfig_Throws()
    {
        var app = App();
        Assert.Throws<ArgumentNullException>(() => UpdatePolicy.ShouldAutoApply(null!, app));
    }

    [Fact]
    public void ShouldAutoApply_NullApp_Throws()
    {
        var cfg = Config();
        Assert.Throws<ArgumentNullException>(() => UpdatePolicy.ShouldAutoApply(cfg, null!));
    }
}