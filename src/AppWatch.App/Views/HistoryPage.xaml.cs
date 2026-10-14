using System.Collections.ObjectModel;
using AppWatch.Infrastructure;
using AppWatch.Localization;
using AppWatch.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AppWatch.Views;

/// <summary>
/// Update history page (ZuiV2 subpage pattern: back header, section, card list).
/// Reloads from the store on every navigation (no cache) so fresh updates show.
/// </summary>
public sealed partial class HistoryPage : Page
{
    private readonly UpdateHistoryStore _store;

    public ObservableCollection<HistoryItem> Entries { get; } = [];

    public Loc Loc => Loc.Instance;

    public Visibility EmptyStateVisible => Entries.Count == 0
        ? Visibility.Visible
        : Visibility.Collapsed;

    public HistoryPage()
    {
        _store = App.Resolve<UpdateHistoryStore>();
        InitializeComponent();
        Reload();
    }

    private void Reload()
    {
        Entries.Clear();
        // newest first
        foreach (var entry in _store.Load().OrderByDescending(e => e.AtUtc))
            Entries.Add(new HistoryItem(entry));
        // empty-state has no converter: x:Bind re-evaluates when page is constructed
    }

    private void Back_Click(object sender, RoutedEventArgs e) =>
        App.MainWindow!.GoBackWithAnimation();
}