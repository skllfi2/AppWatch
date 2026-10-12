using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;

namespace AppWatch.Platform;

/// <summary>
/// Manages page transitions with slide animations.
/// Ported from ZuiV2 Platform/PageTransitionManager.cs (our own code);
/// the SoundService call is not ported (no sound service in AppWatch).
/// </summary>
public sealed class PageTransitionManager
{
    private readonly Frame _frame;

    public PageTransitionManager(Frame frame) => _frame = frame;

    /// <summary>Navigates to a page with slide transition.</summary>
    public void NavigateTo(Type pageType, string? tag = null)
    {
        _frame.Navigate(pageType, null, new SlideNavigationTransitionInfo
        {
            Effect = SlideNavigationTransitionEffect.FromRight,
        });
    }

    /// <summary>Navigates back with reverse slide transition.</summary>
    public bool NavigateBack()
    {
        if (!_frame.CanGoBack)
            return false;

        _frame.GoBack(new SlideNavigationTransitionInfo
        {
            Effect = SlideNavigationTransitionEffect.FromLeft,
        });
        return true;
    }
}
