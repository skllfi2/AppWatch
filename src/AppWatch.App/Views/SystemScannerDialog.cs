using AppWatch.Catalog;
using AppWatch.Core.Domain.Models;
using AppWatch.Infrastructure;
using AppWatch.Localization;
using AppWatch.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace AppWatch.Views;

public sealed class SystemScannerDialog : ContentDialog
{
    private readonly SystemScannerDialogViewModel _viewModel;
    private readonly ListView _listView;
    private readonly TextBlock _statusBlock;
    private readonly ProgressBar _progressBar;

    public SystemScannerDialogViewModel ViewModel => _viewModel;

    public SystemScannerDialog(
        SystemVersionResolver systemVersion,
        CatalogService catalog,
        IReadOnlyList<TrackedApp> existingApps)
    {
        _viewModel = new SystemScannerDialogViewModel(systemVersion, catalog, existingApps);

        Title = Loc.Instance.SystemScannerTitle;
        PrimaryButtonText = Loc.Instance.Format("SystemScannerAddSelected", 0);
        SecondaryButtonText = Loc.Instance["Cancel"];
        DefaultButton = ContentDialogButton.Primary;
        IsPrimaryButtonEnabled = false;

        MinWidth = 660;
        MinHeight = 520;

        _statusBlock = new TextBlock
        {
            FontSize = 12,
            Opacity = 0.7,
            VerticalAlignment = VerticalAlignment.Center
        };

        _progressBar = new ProgressBar
        {
            IsIndeterminate = true,
            Visibility = Visibility.Collapsed,
            Margin = new Thickness(0, 4, 0, 4)
        };

        _listView = new ListView
        {
            SelectionMode = ListViewSelectionMode.None,
            Height = 360,
            ItemsSource = _viewModel.FilteredCandidates
        };

        ConfigureListView();
        Content = BuildContent();

        // Bind ViewModel events
        _viewModel.PropertyChanged += (_, e) =>
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                if (e.PropertyName == nameof(SystemScannerDialogViewModel.CanAdd) ||
                    e.PropertyName == nameof(SystemScannerDialogViewModel.SelectedCount))
                {
                    IsPrimaryButtonEnabled = _viewModel.CanAdd;
                    PrimaryButtonText = Loc.Instance.Format("SystemScannerAddSelected", _viewModel.SelectedCount);
                }
                else if (e.PropertyName == nameof(SystemScannerDialogViewModel.IsScanning))
                {
                    _progressBar.Visibility = _viewModel.IsScanning ? Visibility.Visible : Visibility.Collapsed;
                }
                else if (e.PropertyName == nameof(SystemScannerDialogViewModel.ScanStatusText))
                {
                    _statusBlock.Text = _viewModel.ScanStatusText;
                }
            });
        };

        // Start scan immediately on loaded
        Loaded += async (_, _) =>
        {
            await _viewModel.ScanAsync();
        };
    }

    private Grid BuildContent()
    {
        var root = new Grid
        {
            RowSpacing = 10,
            Padding = new Thickness(0, 6, 0, 0),
            Width = 620
        };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Subtitle
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Controls (Search + Filters)
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Progress
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // List

        // 0. Subtitle
        var subTitle = new TextBlock
        {
            Text = Loc.Instance.SystemScannerSubtitle,
            FontSize = 12,
            Opacity = 0.75,
            TextWrapping = TextWrapping.Wrap
        };
        Grid.SetRow(subTitle, 0);
        root.Children.Add(subTitle);

        // 1. Controls (Search Box + Checkbox + Select buttons)
        var controlsGrid = new Grid { ColumnSpacing = 8 };
        controlsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        controlsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        controlsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        controlsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var searchBox = new TextBox
        {
            PlaceholderText = Loc.Instance.SystemScannerFilterPlaceholder,
            FontSize = 12
        };
        searchBox.TextChanged += (s, e) => _viewModel.SearchFilter = searchBox.Text;
        Grid.SetColumn(searchBox, 0);
        controlsGrid.Children.Add(searchBox);

        var catalogCheck = new CheckBox
        {
            Content = Loc.Instance.SystemScannerCatalogOnly,
            IsChecked = _viewModel.OnlyCatalogMatches,
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center
        };
        catalogCheck.Checked += (_, _) => _viewModel.OnlyCatalogMatches = true;
        catalogCheck.Unchecked += (_, _) => _viewModel.OnlyCatalogMatches = false;
        Grid.SetColumn(catalogCheck, 1);
        controlsGrid.Children.Add(catalogCheck);

        var selectAllBtn = new Button
        {
            Content = Loc.Instance.SystemScannerSelectAll,
            FontSize = 12,
            Padding = new Thickness(8, 5, 8, 5)
        };
        selectAllBtn.Click += (_, _) => _viewModel.SelectAll(true);
        Grid.SetColumn(selectAllBtn, 2);
        controlsGrid.Children.Add(selectAllBtn);

        var deselectAllBtn = new Button
        {
            Content = Loc.Instance.SystemScannerDeselectAll,
            FontSize = 12,
            Padding = new Thickness(8, 5, 8, 5)
        };
        deselectAllBtn.Click += (_, _) => _viewModel.SelectAll(false);
        Grid.SetColumn(deselectAllBtn, 3);
        controlsGrid.Children.Add(deselectAllBtn);

        Grid.SetRow(controlsGrid, 1);
        root.Children.Add(controlsGrid);

        // 2. Status & Progress
        var statusPanel = new StackPanel { Spacing = 4 };
        statusPanel.Children.Add(_progressBar);
        statusPanel.Children.Add(_statusBlock);
        Grid.SetRow(statusPanel, 2);
        root.Children.Add(statusPanel);

        // 3. List
        var border = new Border
        {
            BorderThickness = new Thickness(1),
            BorderBrush = (Brush)Application.Current.Resources["BorderDefaultBrush"],
            CornerRadius = new CornerRadius(8),
            Background = (Brush)Application.Current.Resources["CardBackgroundBrush"],
            Child = _listView
        };
        Grid.SetRow(border, 3);
        root.Children.Add(border);

        return root;
    }

    private void ConfigureListView()
    {
        // Custom UI generator for list items
        _listView.ItemTemplate = null; // using direct DataTemplate or container styling
        // In WinUI 3, we can create a clean DataTemplate or use ContainerContentChanging
        _listView.ContainerContentChanging += (lv, args) =>
        {
            if (args.Handled) return;
            if (args.Item is not SystemScanCandidate candidate) return;

            var container = args.ItemContainer;
            var itemGrid = new Grid
            {
                ColumnSpacing = 10,
                Padding = new Thickness(6, 6, 6, 6),
                Width = 590
            };
            itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Checkbox
            itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Monogram
            itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Info
            itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Badge

            // Checkbox
            var chk = new CheckBox
            {
                IsChecked = candidate.IsSelected,
                IsEnabled = !candidate.IsAlreadyTracked,
                VerticalAlignment = VerticalAlignment.Center
            };
            chk.Checked += (_, _) => candidate.IsSelected = true;
            chk.Unchecked += (_, _) => candidate.IsSelected = false;
            Grid.SetColumn(chk, 0);
            itemGrid.Children.Add(chk);

            // Monogram
            var monoBorder = new Border
            {
                Width = 32,
                Height = 32,
                CornerRadius = new CornerRadius(6),
                Background = (Brush)Application.Current.Resources["HeroOverlayBrush"],
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = candidate.Monogram,
                    FontSize = 13,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            Grid.SetColumn(monoBorder, 1);
            itemGrid.Children.Add(monoBorder);

            // Info Stack
            var infoStack = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
            var nameBlock = new TextBlock
            {
                Text = candidate.DisplayName,
                FontSize = 13,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            var subBlock = new TextBlock
            {
                Text = candidate.Subtitle,
                FontSize = 11,
                Opacity = 0.65,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            infoStack.Children.Add(nameBlock);
            infoStack.Children.Add(subBlock);
            Grid.SetColumn(infoStack, 2);
            itemGrid.Children.Add(infoStack);

            // Status / Source badge
            var badgePanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Spacing = 2,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            if (candidate.IsAlreadyTracked)
            {
                var trackedBadge = new Border
                {
                    Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(35, 255, 185, 0)),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(6, 2, 6, 2),
                    Child = new TextBlock
                    {
                        Text = Loc.Instance.SystemScannerAlreadyTracked,
                        FontSize = 10,
                        Foreground = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 255, 185, 0))
                    }
                };
                badgePanel.Children.Add(trackedBadge);
            }
            else if (candidate.IsCatalogMatch)
            {
                var catBadge = new Border
                {
                    Background = (Brush)Application.Current.Resources["HeroOverlayBrush"],
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(6, 2, 6, 2),
                    Child = new TextBlock
                    {
                        Text = candidate.SourceLabel,
                        FontSize = 10,
                        Foreground = (Brush)Application.Current.Resources["AccentBrush"]
                    }
                };
                badgePanel.Children.Add(catBadge);
            }

            Grid.SetColumn(badgePanel, 3);
            itemGrid.Children.Add(badgePanel);

            container.Content = itemGrid;
            args.Handled = true;
        };
    }
}
