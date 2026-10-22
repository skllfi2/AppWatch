using AppWatch.Localization;
using AppWatch.ViewModels;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using System.Numerics;

namespace AppWatch.Views;

/// <summary>
/// Combined Home + Catalog page with TabView.
/// Tab 0 = Home (tracked apps with groups), Tab 1 = Catalog (search/install).
/// Tab switch animates content in (fade + slide).
/// </summary>
public sealed partial class HomeCatalogPage : Page
{
    public MainViewModel ViewModel { get; }

    public Loc Loc => Loc.Instance;

    public HomeCatalogPage()
    {
        ViewModel = App.Resolve<MainViewModel>();
        InitializeComponent();
        Tabs.SelectionChanged += OnTabSelectionChanged;
        Loaded += (_, _) => AnimateSelectedTabContent();
    }

    private void OnTabSelectionChanged(object sender, SelectionChangedEventArgs args)
            {
                if (args.AddedItems.Count > 0 && args.AddedItems[0] is TabViewItem item)
                    AnimateTabItemContent(item);
            }

    private void AnimateSelectedTabContent()
    {
        if (Tabs.SelectedItem is TabViewItem item)
            AnimateTabItemContent(item);
    }

    private static void AnimateTabItemContent(TabViewItem item)
        {
            if (item.Content is not FrameworkElement el) return;

            // Composition animation (no Storyboard property-path parsing —
            // "RenderTransform.Y" paths crash WinUI 3 with COMException).
            var visual = ElementCompositionPreview.GetElementVisual(el);
            var compositor = visual.Compositor;

            var easing = compositor.CreateCubicBezierEasingFunction(
                new Vector2(0.0f, 0.0f), new Vector2(0.25f, 1.0f));
            var duration = TimeSpan.FromMilliseconds(280);

            var fade = compositor.CreateScalarKeyFrameAnimation();
            fade.Target = "Opacity";
            fade.InsertKeyFrame(1.0f, 1.0f, easing);
            fade.Duration = duration;

            var slide = compositor.CreateVector3KeyFrameAnimation();
            slide.Target = "Offset";
            slide.InsertKeyFrame(1.0f, new Vector3(0f, 0f, 0f), easing);
            slide.Duration = duration;

            // start state: transparent, shifted down 14px
            visual.Opacity = 0f;
            visual.Offset = new Vector3(0f, 14f, 0f);

            visual.StartAnimation("Opacity", fade);
            visual.StartAnimation("Offset", slide);
        }
}