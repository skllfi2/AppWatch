using AppWatch.Catalog;
using AppWatch.Core.Domain.Models;
using AppWatch.Localization;
using AppWatch.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Storage.Pickers;

namespace AppWatch.Views;

public sealed class BatchImportDialog : ContentDialog
{
    private readonly BatchImportDialogViewModel _viewModel;
    private readonly ListView _listView;
    private readonly TextBox _inputBox;
    private readonly TextBlock _summaryBlock;

    public BatchImportDialogViewModel ViewModel => _viewModel;

    public BatchImportDialog(
        IReadOnlyList<TrackedApp> existingApps,
        CatalogService catalog,
        string? initialText = null)
    {
        _viewModel = new BatchImportDialogViewModel(existingApps, catalog, initialText);

        Title = Loc.Instance.BatchImportTitle;
        PrimaryButtonText = Loc.Instance.Format("BatchImportImportSelected", 0);
        SecondaryButtonText = Loc.Instance["Cancel"];
        DefaultButton = ContentDialogButton.Primary;
        IsPrimaryButtonEnabled = false;

        MinWidth = 640;
        MinHeight = 540;

        _inputBox = new TextBox
        {
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            Height = 120,
            PlaceholderText = Loc.Instance.BatchImportPlaceholder,
            FontFamily = new FontFamily("Consolas, Segoe UI Variable"),
            FontSize = 12,
            Text = _viewModel.RawText
        };
        _inputBox.TextChanged += (_, _) => _viewModel.RawText = _inputBox.Text;

        _summaryBlock = new TextBlock
        {
            FontSize = 12,
            Opacity = 0.7,
            VerticalAlignment = VerticalAlignment.Center
        };

        _listView = new ListView
        {
            SelectionMode = ListViewSelectionMode.None,
            Height = 240,
            ItemsSource = _viewModel.Candidates
        };

        ConfigureListView();
        Content = BuildContent();

        _viewModel.PropertyChanged += (_, e) =>
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                if (e.PropertyName == nameof(BatchImportDialogViewModel.CanImport) ||
                    e.PropertyName == nameof(BatchImportDialogViewModel.SelectedCount))
                {
                    IsPrimaryButtonEnabled = _viewModel.CanImport;
                    PrimaryButtonText = Loc.Instance.Format("BatchImportImportSelected", _viewModel.SelectedCount);
                }
                else if (e.PropertyName == nameof(BatchImportDialogViewModel.AnalysisSummary))
                {
                    _summaryBlock.Text = _viewModel.AnalysisSummary;
                }
            });
        };
    }

    private Grid BuildContent()
    {
        var root = new Grid
        {
            RowSpacing = 10,
            Padding = new Thickness(0, 6, 0, 0),
            Width = 600
        };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Subtitle
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // TextBox
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Buttons row
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Summary
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Preview List

        // 0. Subtitle
        var subTitle = new TextBlock
        {
            Text = Loc.Instance.BatchImportSubtitle,
            FontSize = 12,
            Opacity = 0.75,
            TextWrapping = TextWrapping.Wrap
        };
        Grid.SetRow(subTitle, 0);
        root.Children.Add(subTitle);

        // 1. Text input
        Grid.SetRow(_inputBox, 1);
        root.Children.Add(_inputBox);

        // 2. Actions row (File Picker, Parse button, Select All, Deselect All)
        var actionsGrid = new Grid { ColumnSpacing = 8 };
        actionsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        actionsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        actionsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        actionsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        actionsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var loadFileBtn = new Button
        {
            Content = Loc.Instance.BatchImportLoadFile,
            FontSize = 12,
            Padding = new Thickness(10, 5, 10, 5)
        };
        loadFileBtn.Click += async (_, _) => await PickFileAsync();
        Grid.SetColumn(loadFileBtn, 0);
        actionsGrid.Children.Add(loadFileBtn);

        var parseBtn = new Button
        {
            Content = Loc.Instance.BatchImportParseBtn,
            FontSize = 12,
            Padding = new Thickness(12, 5, 12, 5),
            Background = (Brush)Application.Current.Resources["HeroOverlayBrush"]
        };
        parseBtn.Click += (_, _) => _viewModel.Analyze();
        Grid.SetColumn(parseBtn, 1);
        actionsGrid.Children.Add(parseBtn);

        var selectAllBtn = new Button
        {
            Content = Loc.Instance.SystemScannerSelectAll,
            FontSize = 12,
            Padding = new Thickness(8, 5, 8, 5)
        };
        selectAllBtn.Click += (_, _) => _viewModel.SelectAll(true);
        Grid.SetColumn(selectAllBtn, 3);
        actionsGrid.Children.Add(selectAllBtn);

        var deselectAllBtn = new Button
        {
            Content = Loc.Instance.SystemScannerDeselectAll,
            FontSize = 12,
            Padding = new Thickness(8, 5, 8, 5)
        };
        deselectAllBtn.Click += (_, _) => _viewModel.SelectAll(false);
        Grid.SetColumn(deselectAllBtn, 4);
        actionsGrid.Children.Add(deselectAllBtn);

        Grid.SetRow(actionsGrid, 2);
        root.Children.Add(actionsGrid);

        // 3. Summary
        Grid.SetRow(_summaryBlock, 3);
        root.Children.Add(_summaryBlock);

        // 4. Preview List
        var listBorder = new Border
        {
            BorderThickness = new Thickness(1),
            BorderBrush = (Brush)Application.Current.Resources["BorderDefaultBrush"],
            CornerRadius = new CornerRadius(8),
            Background = (Brush)Application.Current.Resources["CardBackgroundBrush"],
            Child = _listView
        };
        Grid.SetRow(listBorder, 4);
        root.Children.Add(listBorder);

        return root;
    }

    private async Task PickFileAsync()
    {
        var picker = new FileOpenPicker();
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow!);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        picker.FileTypeFilter.Add(".txt");
        picker.FileTypeFilter.Add(".json");
        picker.FileTypeFilter.Add(".md");

        var file = await picker.PickSingleFileAsync();
        if (file is not null)
        {
            var text = await Windows.Storage.FileIO.ReadTextAsync(file);
            _inputBox.Text = text;
            _viewModel.RawText = text;
            _viewModel.Analyze();
        }
    }

    private void ConfigureListView()
    {
        _listView.ContainerContentChanging += (lv, args) =>
        {
            if (args.Handled) return;
            if (args.Item is not BatchImportCandidate candidate) return;

            var container = args.ItemContainer;
            var itemGrid = new Grid
            {
                ColumnSpacing = 10,
                Padding = new Thickness(6, 6, 6, 6),
                Width = 570
            };
            itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Checkbox
            var chk = new CheckBox
            {
                IsChecked = candidate.IsSelected,
                IsEnabled = candidate.IsValid && !candidate.IsDuplicate,
                VerticalAlignment = VerticalAlignment.Center
            };
            chk.Checked += (_, _) => candidate.IsSelected = true;
            chk.Unchecked += (_, _) => candidate.IsSelected = false;
            Grid.SetColumn(chk, 0);
            itemGrid.Children.Add(chk);

            // Name and Raw input
            var infoStack = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
            var nameBlock = new TextBlock
            {
                Text = candidate.Name,
                FontSize = 13,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            var rawBlock = new TextBlock
            {
                Text = candidate.SourceBadge,
                FontSize = 11,
                Opacity = 0.65,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            infoStack.Children.Add(nameBlock);
            infoStack.Children.Add(rawBlock);
            Grid.SetColumn(infoStack, 1);
            itemGrid.Children.Add(infoStack);

            // Status badge
            var badgeBorder = new Border
            {
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 2, 6, 2),
                VerticalAlignment = VerticalAlignment.Center
            };
            var statusText = new TextBlock { Text = candidate.StatusText, FontSize = 11 };

            if (!candidate.IsValid)
            {
                badgeBorder.Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(35, 232, 17, 35));
                statusText.Foreground = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 232, 17, 35));
            }
            else if (candidate.IsDuplicate)
            {
                badgeBorder.Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(35, 255, 185, 0));
                statusText.Foreground = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 255, 185, 0));
            }
            else
            {
                badgeBorder.Background = (Brush)Application.Current.Resources["HeroOverlayBrush"];
                statusText.Foreground = (Brush)Application.Current.Resources["AccentBrush"];
            }

            badgeBorder.Child = statusText;
            Grid.SetColumn(badgeBorder, 2);
            itemGrid.Children.Add(badgeBorder);

            container.Content = itemGrid;
            args.Handled = true;
        };
    }
}
