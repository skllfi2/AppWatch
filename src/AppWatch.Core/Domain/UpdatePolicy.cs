using AppWatch.Core.Domain.Models;

namespace AppWatch.Core.Domain;

/// <summary>
/// P1: Pure consent gate for auto-applying updates.
/// Default OFF; respects AppConfig.AutoApplyUpdates, TrackedApp.Enabled, and TrackedApp.LaunchAfterUpdate.
/// Both BackgroundMonitor and MainViewModel.RunBackgroundCheckAsync should call this.
/// </summary>
public static class UpdatePolicy
{
    public static bool ShouldAutoApply(AppConfig config, TrackedApp app)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(app);

        return config.AutoApplyUpdates
            && app.Enabled
            && app.LaunchAfterUpdate;
    }
}