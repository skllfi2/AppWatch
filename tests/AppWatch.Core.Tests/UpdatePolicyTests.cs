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
            // Trust model: per-app consent must be EXPLICIT — default OFF
            AutoApplyConsent = true,
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

    // Trust model: per-app consent gates auto-apply — the global flag alone is not enough.
    // A user-imported stars entry defaults to consent OFF → never auto-applies.
    [Fact]
    public void ShouldAutoApply_PerAppConsentOff_ReturnsFalse_EvenWithGlobalFlag()
    {
        var cfg = Config(c => c.AutoApplyUpdates = true);
        var app = App(a => a.AutoApplyConsent = false);
        Assert.False(UpdatePolicy.ShouldAutoApply(cfg, app));
    }

    [Fact]
    public void ShouldAutoApply_PerAppConsentOn_AllFlagsOn_ReturnsTrue()
    {
        var cfg = Config(c => c.AutoApplyUpdates = true);
        var app = App(a => a.AutoApplyConsent = true);
        Assert.True(UpdatePolicy.ShouldAutoApply(cfg, app));
    }

    [Fact]
    public void ShouldAutoApply_NewApp_DefaultConsentOff_NeverAutoApplies()
    {
        var cfg = Config(c => c.AutoApplyUpdates = true);
        var fresh = new TrackedApp { Name = "Fresh", RepoOwner = "o", RepoName = "r" };
        Assert.False(fresh.AutoApplyConsent); // safe-by-default invariant
        Assert.False(UpdatePolicy.ShouldAutoApply(cfg, fresh));
    }
}