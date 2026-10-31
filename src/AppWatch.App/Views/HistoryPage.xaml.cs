using System.Collections.ObjectModel;
using System.ComponentModel;
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
public sealed partial class HistoryPage : Page, INotifyPropertyChanged
{
    private readonly UpdateHistoryStore _store;
    private readonly List<HistoryItem> _allEntries = [];

    public ObservableCollection<HistoryItem> FilteredEntries { get; } = [];

    public Loc Loc => Loc.Instance;

    private string _searchQuery = "";
    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (_searchQuery != value)
            {
                _searchQuery = value;
                OnPropertyChanged(nameof(SearchQuery));
                ApplyFilter();
            }
        }
    }

    private int _selectedFilterIndex;
    public int SelectedFilterIndex
    {
        get => _selectedFilterIndex;
        set
        {
            if (_selectedFilterIndex != value)
            {
                _selectedFilterIndex = value;
                OnPropertyChanged(nameof(SelectedFilterIndex));
                ApplyFilter();
            }
        }
    }

    public Visibility EmptyStateVisible => FilteredEntries.Count == 0
        ? Visibility.Visible
        : Visibility.Collapsed;

    public bool HasEntries => _allEntries.Count > 0;

    public string EmptyStateMessage => _allEntries.Count == 0
        ? Loc.HistoryEmpty
        : Loc.HistoryNoMatches;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    public HistoryPage()
    {
        _store = App.Resolve<UpdateHistoryStore>();
        InitializeComponent();
        Reload();
    }

    protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        Reload();
    }

    private void Reload()
    {
        _allEntries.Clear();
        // newest first
        foreach (var entry in _store.Load().OrderByDescending(e => e.AtUtc))
            _allEntries.Add(new HistoryItem(entry));
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        FilteredEntries.Clear();
        IEnumerable<HistoryItem> query = _allEntries;

        if (SelectedFilterIndex == 1)
            query = query.Where(e => e.IsSuccess);
        else if (SelectedFilterIndex == 2)
            query = query.Where(e => !e.IsSuccess);

        if (!string.IsNullOrWhiteSpace(SearchQuery))
        {
            var q = SearchQuery.Trim();
            query = query.Where(e => e.AppName.Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var item in query)
            FilteredEntries.Add(item);

        OnPropertyChanged(nameof(EmptyStateVisible));
        OnPropertyChanged(nameof(HasEntries));
        OnPropertyChanged(nameof(EmptyStateMessage));
    }

    private void ClearHistory_Click(object sender, RoutedEventArgs e)
    {
        _store.Clear();
        _allEntries.Clear();
        ApplyFilter();
    }
}
