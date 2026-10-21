using AppWatch.Catalog;
using AppWatch.Core.Domain.Models;
using AppWatch.Localization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AppWatch.ViewModels;

/// <summary>
/// Wrapper VM for CatalogSource so x:Bind inside the DataTemplate can reach
/// localized strings and the remove command (the project's established pattern —
/// same as CatalogItem proxying commands for x:Bind).
/// </summary>
public partial class CatalogSourceItem : ObservableObject
{
    private readonly CatalogService _catalog;

    public CatalogSourceItem(CatalogSource source, CatalogService catalog)
    {
        Source = source;
        _catalog = catalog;
        // Set via property: OnEnabledChanged guard (Source.Enabled == value) prevents
        // the persistence call at construction time.
        Enabled = source.Enabled;
    }

    /// <summary>Raw domain model — passed as CommandParameter to the remove command.</summary>
    public CatalogSource Source { get; }

    public string Name => Source.Name;

    public string? Url => Source.Url;

    /// <summary>Seed sources cannot be removed — hide the button.</summary>
    public bool CanRemove => Source.Type != CatalogSourceType.Seed;

    public string SourceEnabled => LocalizationService.Instance["SourceEnabled"];

    public string SourceDisabled => LocalizationService.Instance["SourceDisabled"];

    public string BtnRemoveSource => LocalizationService.Instance["BtnRemoveSource"];

    [ObservableProperty]
    public partial bool Enabled { get; set; }

    partial void OnEnabledChanged(bool value)
    {
        // ToggleSource persists the new state; only act when it actually differs
        // (TwoWay binding writes back the initial value on load).
        if (Source.Enabled != value)
            _catalog.ToggleSource(Source.Id);
    }

    /// <summary>Remove command proxied from the shared MainViewModel (x:Bind inside DataTemplate).</summary>
    public IRelayCommand RemoveCatalogSourceCommand =>
        global::AppWatch.App.Resolve<MainViewModel>().RemoveCatalogSourceCommand;
}
