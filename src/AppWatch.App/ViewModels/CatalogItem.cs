using AppWatch.Core.Domain.Models;
using AppWatch.Localization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AppWatch.ViewModels;

public partial class CatalogItem : ObservableObject
{
    private static readonly uint[] MonogramPalette =
    [
        0xFF60CDFF, 0xFF4CAF50, 0xFF9C27B0, 0xFFFB5E5E,
        0xFFFFB900, 0xFF616161, 0xFF0078D4, 0xFF9E9E9E,
    ];

    public CatalogItem(CatalogEntry entry, bool isTracked)
    {
        Entry = entry;
        IsTracked = isTracked;
    }

    public CatalogEntry Entry { get; }

    public string Name => Entry.Name;

    public string Description => Entry.Description ?? "";

    public string Monogram => string.IsNullOrEmpty(Name) ? "?" : Name[..1].ToUpperInvariant();

    public Microsoft.UI.Xaml.Media.Brush MonogramBrush
    {
        get
        {
            var hash = Math.Abs(string.GetHashCode(Name, StringComparison.OrdinalIgnoreCase));
            var hex = MonogramPalette[hash % MonogramPalette.Length];
            return new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Microsoft.UI.ColorHelper.FromArgb(255, (byte)(hex >> 16), (byte)(hex >> 8), (byte)hex));
        }
    }

    /// <summary>Localized install-button label (kept on the item so x:Bind inside DataTemplate resolves).</summary>
    public string InstallButtonText => LocalizationService.Instance["install"];

    /// <summary>Install command proxied from the shared MainViewModel (x:Bind inside DataTemplate).</summary>
    public IRelayCommand InstallFromCatalogCommand =>
        global::AppWatch.App.Resolve<MainViewModel>().InstallFromCatalogCommand;

    [ObservableProperty]
    public partial bool IsTracked { get; set; }

    [ObservableProperty]
    public partial string InstallStatus { get; set; } = "";

    [ObservableProperty]
    public partial bool UpdateAvailable { get; set; }
}
