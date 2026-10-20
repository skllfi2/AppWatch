using AppWatch.Core.Domain.Models;

namespace AppWatch.Core.Domain;

/// <summary>
/// P1: Pure consent gate for auto-applying updates.
/// Safe by default: requires the GLOBAL flag, per-app Enabled, AND per-app
/// AutoApplyConsent (trust model — no unattended updates without explicit
/// opt-in for that specific app; user-imported stars entries never auto-apply
/// until the user consents). Both BackgroundMonitor and MainViewModel
/// should call this.
/// </summary>
public static class UpdatePolicy
{
    public static bool ShouldAutoApply(AppConfig config, TrackedApp app)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(app);

        return config.AutoApplyUpdates
            && app.Enabled
            && app.AutoApplyConsent
            && app.LaunchAfterUpdate;
    }
}