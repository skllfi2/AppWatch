using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using AppWatch.Catalog;
using AppWatch.Core.Domain;
using AppWatch.Core.Domain.Models;
using AppWatch.Infrastructure;
using AppWatch.Localization;

namespace AppWatch.Views;

/// <summary>
/// Import from GitHub Stars: username (+ optional PAT token) → fetch starred repos,
/// check their latest releases for Windows assets → checkbox list with the
/// auto-detected update kind → add the selection to the user catalog.
/// NOTE: the item template uses {Binding} — x:Bind does NOT work in XamlReader.Load.
/// </summary>
public sealed class ImportFromStarsDialog : ContentDialog
{
    private GitHubStarsService _stars = new();
    private readonly CatalogService _catalog;
    private readonly Loc _loc;

    public ObservableCollection<StarImportItem> Items { get; } = new();

    private TextBox _nameBox = null!;
    private TextBox _tokenBox = null!;
    private Button _loadBtn = null!;
    private TextBlock _statusText = null!;
    private TextBlock _errorText = null!;
    private ListView _list = null!;
    private CheckBox _selectAllBox = null!;
    private TextBlock _selectedCount = null!;

    public ImportFromStarsDialog(CatalogService catalog)
    {
        _catalog = catalog;
        _loc = Loc.Instance;

        Title = _loc["ImportFromStarsTitle"];
        PrimaryButtonText = _loc["BtnImport"];
        SecondaryButtonText = _loc["Cancel"];
        DefaultButton = ContentDialogButton.Primary;
        MinWidth = 620;

        try
        {
            Content = BuildContent();
        }
        catch (System.Exception ex)
        {
            // Diagnostics: show the exact failure instead of killing the process
            Content = new ScrollViewer
            {
                Content = new TextBlock { Text = ex.ToString(), TextWrapping = TextWrapping.Wrap, MaxHeight = 420 },
            };
        }
    }

    private ScrollViewer BuildContent()
    {
        var panel = new StackPanel { Spacing = 10 };

        // Username + optional PAT token
        panel.Children.Add(new TextBlock
        {
            Text = _loc["ImportStarsUsernameHint"],
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
            Opacity = 0.7,
        });

        _nameBox = new TextBox { PlaceholderText = "skllfi2", Header = "GitHub username" };
        panel.Children.Add(_nameBox);

        _tokenBox = new TextBox
        {
            PlaceholderText = _loc["ImportStarsTokenHint"],
            Header = _loc["ImportStarsTokenHeader"],
        };
        panel.Children.Add(_tokenBox);

        // Load button + status
        // NOTE: theme resources (AccentFillDefaultBrush etc.) are NOT reachable via
        // Application.Current.Resources in code — that lookup throws COMException
        // 0x80004005 and crashes the process. Default button style is used instead.
        _loadBtn = new Button
        {
            Content = _loc["BtnLoadStars"],
            Padding = new Thickness(16, 8, 16, 8),
        };
        _loadBtn.Click += async (_, _) => await LoadStarsAsync();
        panel.Children.Add(_loadBtn);

        _statusText = new TextBlock { Text = "", Opacity = 0.7, FontSize = 12, TextWrapping = TextWrapping.Wrap };
        panel.Children.Add(_statusText);

        _errorText = new TextBlock
        {
            Text = "", FontSize = 12, TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Microsoft.UI.Colors.Firebrick), Visibility = Visibility.Collapsed,
        };
        panel.Children.Add(_errorText);

        // Select all + counter
        var selectRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        _selectAllBox = new CheckBox { Content = _loc["ImportStarsSelectAll"], IsChecked = true };
        _selectAllBox.Click += (_, _) =>
        {
            foreach (var item in Items)
                item.Selected = _selectAllBox.IsChecked == true;
            UpdateSelectedCount();
        };
        selectRow.Children.Add(_selectAllBox);
        _selectedCount = new TextBlock { FontSize = 12, Opacity = 0.7, VerticalAlignment = VerticalAlignment.Center };
        selectRow.Children.Add(_selectedCount);
        panel.Children.Add(selectRow);

        // List — {Binding} only: x:Bind is a compile-time feature and fails in XamlReader.Load.
        // Also: no {ThemeResource} in the runtime template — the parser context has no
        // theme dictionaries; every style value must be self-contained.
        _list = new ListView
        {
            SelectionMode = ListViewSelectionMode.None,
            MaxHeight = 420,
            ItemsSource = Items,
            ItemTemplate = (DataTemplate)Microsoft.UI.Xaml.Markup.XamlReader.Load(
@"<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'>
    <Grid Padding='8,6' ColumnSpacing='10'>
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width='Auto'/>
            <ColumnDefinition Width='*'/>
            <ColumnDefinition Width='Auto'/>
        </Grid.ColumnDefinitions>
        <CheckBox Grid.Column='0' IsChecked='{Binding Selected, Mode=TwoWay}' VerticalAlignment='Center'/>
        <StackPanel Grid.Column='1' VerticalAlignment='Center' Spacing='2'>
            <TextBlock Text='{Binding RepoName}' FontWeight='SemiBold' FontSize='13'/>
            <TextBlock Text='{Binding OwnerLogin}' FontSize='11' Opacity='0.6'/>
            <TextBlock Text='{Binding Description}' FontSize='11' Opacity='0.5' TextWrapping='Wrap'
                       MaxLines='2' TextTrimming='CharacterEllipsis'/>
        </StackPanel>
        <StackPanel Grid.Column='2' VerticalAlignment='Center' Spacing='2' Opacity='0.7'>
            <TextBlock Text='{Binding SuggestedKindName}' FontSize='10'/>
            <TextBlock Text='{Binding SuggestedExe}' FontSize='10'/>
        </StackPanel>
    </Grid>
</DataTemplate>"),
        };
        panel.Children.Add(_list);

        return new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }

    private async Task LoadStarsAsync()
    {
        var username = _nameBox.Text.Trim();
        if (username.Length == 0) return;

        _loadBtn.IsEnabled = false;
        Items.Clear();
        _errorText.Visibility = Visibility.Collapsed;
        _errorText.Text = "";
        _statusText.Text = _loc["ImportStarsFetching"];

        _stars = new GitHubStarsService(_tokenBox.Text);

        try
        {
            var repos = await _stars.GetStarredReposAsync(username);
            var candidates = repos.Where(r => !r.Fork).ToList();
            var total = candidates.Count;
            var found = 0;
            var checkedCount = 0;

            foreach (var repo in candidates)
            {
                // Stop before burning the last requests of the quota window
                if (_stars.RateLimitExhausted || _stars.RateLimitRemaining <= 2)
                    break;

                checkedCount++;
                _statusText.Text = _loc.Format("ImportStarsProgress", checkedCount, total);

                GitHubRelease? release;
                try
                {
                    release = await _stars.GetLatestReleaseAsync(repo.OwnerLogin, repo.Name);
                }
                catch (HttpRequestException)
                {
                    continue; // individual repo failures are non-fatal
                }

                if (release?.Assets is not { Length: > 0 }) continue;

                var assetNames = release.Assets.Select(a => a.Name).ToList();
                var kind = StarsImportHeuristics.SuggestUpdateKind(assetNames);
                if (kind is null) continue; // no Windows assets

                var exe = StarsImportHeuristics.SuggestExecutableName(assetNames, kind.Value);

                Items.Add(new StarImportItem
                {
                    RepoName = repo.Name,
                    OwnerLogin = repo.OwnerLogin,
                    Description = repo.Description ?? "",
                    Selected = true,
                    SuggestedKind = kind.Value,
                    SuggestedExe = exe ?? "—",
                });
                found++;
            }

            var stoppedByLimit = _stars.RateLimitExhausted || (_stars.RateLimitRemaining <= 2 && checkedCount < total);
            var summary = found > 0
                ? _loc.Format("ImportStarsLoaded", found)
                : (total == 0 ? _loc["ImportStarsNoStars"] : _loc["ImportStarsEmpty"]);
            if (stoppedByLimit)
                summary += "\n" + _loc["ImportStarsRateLimited"];
            _statusText.Text = summary;
        }
        catch (HttpRequestException ex)
        {
            _errorText.Text = _loc.Format("ImportStarsLoadError", ex.Message);
            _errorText.Visibility = Visibility.Visible;
            _statusText.Text = "";
        }
        finally
        {
            _loadBtn.IsEnabled = true;
            UpdateSelectedCount();
        }
    }

    private void UpdateSelectedCount() =>
        _selectedCount.Text = _loc.Format("ImportStarsSelectedCount", Items.Count(i => i.Selected));

    /// <summary>Entries checked by the user, ready for the user catalog.</summary>
    public System.Collections.Generic.List<CatalogEntry> GetSelectedEntries() =>
        Items.Where(i => i.Selected)
            .Select(i => new CatalogEntry
            {
                Name = i.RepoName,
                RepoOwner = i.OwnerLogin,
                RepoName = i.RepoName,
                UpdateKind = i.SuggestedKind,
                ExecutableName = i.SuggestedExe != "—" ? i.SuggestedExe : null,
                Description = string.IsNullOrWhiteSpace(i.Description) ? null : i.Description,
            })
            .ToList();
}

public sealed class StarImportItem : System.ComponentModel.INotifyPropertyChanged
{
    private bool _selected = true;

    public string RepoName { get; set; } = "";
    public string OwnerLogin { get; set; } = "";
    public string Description { get; set; } = "";
    public UpdateKind SuggestedKind { get; set; }
    public string SuggestedKindName => SuggestedKind.ToString();
    public string SuggestedExe { get; set; } = "";

    public bool Selected
    {
        get => _selected;
        set
        {
            _selected = value;
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Selected)));
        }
    }

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
}
