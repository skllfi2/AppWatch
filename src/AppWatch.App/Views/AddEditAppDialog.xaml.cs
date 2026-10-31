using System.Diagnostics;
using AppWatch.Converters;
using AppWatch.Core.Domain.Models;
using AppWatch.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace AppWatch.Views;

/// <summary>
/// Add/Edit custom app dialog — built in C# to avoid XamlCompiler crashes with ContentDialog on RU locale.
///
/// Layout v4 (Smart Add):
///   0. Duplicate warning banner (if already tracked)
///   1. Local file picker button (.exe / .lnk) + detected status
///   2. Repo input (owner/repo or full URL) + Auto-fetch on paste + "Fetch from GitHub" button
///   3. Repo card: avatar, name, description, homepage, "Open in GitHub", Release Asset Selector
///   4. Essentials: Name, UpdateKind, Executable, Process (+ running process selector), InstallDir, WingetId (+ interactive search)
///   5. Options: Launch, Pre-releases, Auto-detect version, Install immediately, Create shortcuts
///   6. Advanced expander: strategy, version-file fields, unsigned consent
/// </summary>
public sealed class AddEditAppDialog : ContentDialog
{
    private readonly AddEditAppDialogViewModel _viewModel;

    public AddEditAppDialog(TrackedApp? app = null)
    {
        _viewModel = new AddEditAppDialogViewModel(app);

        Title = _viewModel.DialogTitle;
        PrimaryButtonText = _viewModel.PrimaryButtonText;
        SecondaryButtonText = AppWatch.Localization.Loc.Instance["Cancel"];
        DefaultButton = ContentDialogButton.Primary;
        CloseButtonText = AppWatch.Localization.Loc.Instance["Cancel"];

        Content = BuildContent();
        MinWidth = 540;

        // Gate primary button
        IsPrimaryButtonEnabled = _viewModel.CanAdd;
        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AddEditAppDialogViewModel.CanAdd))
                IsPrimaryButtonEnabled = _viewModel.CanAdd;
        };
    }

    public AddEditAppDialogViewModel ViewModel => _viewModel;

    private ScrollViewer BuildContent()
    {
        var panel = new StackPanel { Spacing = 14, Padding = new Thickness(0, 8, 0, 0) };

        // ── 0. Duplicate warning banner ──
        var dupBorder = new Border
        {
            Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(40, 255, 185, 0)),
            BorderBrush = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(200, 255, 185, 0)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12, 8, 12, 8),
            Visibility = _viewModel.HasDuplicateWarning ? Visibility.Visible : Visibility.Collapsed,
        };
        var dupStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        dupStack.Children.Add(new FontIcon
        {
            Glyph = "\uE7BA",
            FontSize = 14,
            Foreground = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 255, 185, 0)),
            VerticalAlignment = VerticalAlignment.Center
        });
        var dupText = new TextBlock
        {
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap
        };
        BindLive(dupText, TextBlock.TextProperty, nameof(AddEditAppDialogViewModel.DuplicateWarning));
        dupStack.Children.Add(dupText);
        dupBorder.Child = dupStack;
        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AddEditAppDialogViewModel.DuplicateWarning))
                dupBorder.Visibility = _viewModel.HasDuplicateWarning ? Visibility.Visible : Visibility.Collapsed;
        };
        panel.Children.Add(dupBorder);

        // ── 1. Local file picker button (.exe / .lnk) ──
        if (_viewModel.IsNewApp)
        {
            var pickFileBtn = new Button
            {
                Content = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    Children =
                    {
                        new FontIcon { Glyph = "\uE8B7", FontSize = 14 },
                        new TextBlock { Text = AppWatch.Localization.Loc.Instance["PickLocalFile"] }
                    }
                },
                Padding = new Thickness(14, 8, 14, 8),
                CornerRadius = new CornerRadius(6),
                Background = (Brush)App.Current.Resources["CardBackgroundBrush"],
                BorderThickness = new Thickness(1),
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            pickFileBtn.Click += async (_, _) => await _viewModel.PickLocalFileAsync();
            panel.Children.Add(pickFileBtn);

            var localStatusText = new TextBlock
            {
                FontSize = 12,
                Foreground = (Brush)App.Current.Resources["AccentBrush"],
                Visibility = Visibility.Collapsed,
                Margin = new Thickness(4, -6, 0, 0),
            };
            BindLive(localStatusText, TextBlock.TextProperty, nameof(AddEditAppDialogViewModel.LocalFileStatus));
            _viewModel.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(AddEditAppDialogViewModel.LocalFileStatus))
                    localStatusText.Visibility = !string.IsNullOrWhiteSpace(_viewModel.LocalFileStatus)
                        ? Visibility.Visible : Visibility.Collapsed;
            };
            panel.Children.Add(localStatusText);
        }

        // ── 2. Repo input row ──
        var repoGrid = new Grid { ColumnSpacing = 10 };
        repoGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        repoGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var repoBox = new TextBox
        {
            PlaceholderText = AppWatch.Localization.Loc.Instance["RepoInputPlaceholder"],
            Text = _viewModel.RepoInput,
        };
        repoBox.TextChanged += async (_, _) =>
        {
            _viewModel.RepoInput = repoBox.Text;
            var text = repoBox.Text?.Trim() ?? "";
            // Auto-fetch if full GitHub URL is pasted
            if (text.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                text.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                var (owner, repo) = AddEditAppDialogViewModel.ParseRepoInput(text);
                if (owner is not null && repo is not null && !_viewModel.IsFetchingRepoInfo)
                {
                    await _viewModel.FetchRepoInfoAsync();
                }
            }
        };
        Grid.SetColumn(repoBox, 0);
        repoGrid.Children.Add(repoBox);

        var fetchStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var fetchBtn = new Button
        {
            Content = AppWatch.Localization.Loc.Instance["FetchFromGitHub"],
            Padding = new Thickness(16, 6, 16, 6),
            CornerRadius = new CornerRadius(6),
            Style = (Style)App.Current.Resources["AccentButtonStyle"],
        };
        var fetchRing = new ProgressRing
        {
            Width = 16, Height = 16,
            IsActive = false,
            Visibility = Visibility.Collapsed,
            VerticalAlignment = VerticalAlignment.Center,
        };
        fetchStack.Children.Add(fetchBtn);
        fetchStack.Children.Add(fetchRing);
        fetchBtn.Click += async (_, _) => await _viewModel.FetchRepoInfoAsync();
        Grid.SetColumn(fetchStack, 1);
        repoGrid.Children.Add(fetchStack);

        panel.Children.Add(Field(AppWatch.Localization.Loc.Instance["RepoInputLabel"], repoGrid));

        // Fetch state
        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AddEditAppDialogViewModel.IsFetchingRepoInfo))
            {
                fetchBtn.IsEnabled = !_viewModel.IsFetchingRepoInfo;
                fetchRing.IsActive = _viewModel.IsFetchingRepoInfo;
                fetchRing.Visibility = _viewModel.IsFetchingRepoInfo ? Visibility.Visible : Visibility.Collapsed;
            }
            else if (e.PropertyName == nameof(AddEditAppDialogViewModel.RepoInput))
            {
                if (repoBox.Text != _viewModel.RepoInput)
                    repoBox.Text = _viewModel.RepoInput;
            }
        };

        // Fetch error
        var errorText = new TextBlock
        {
            Foreground = (Brush)App.Current.Resources["ErrorBrush"],
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Visibility = Visibility.Collapsed,
        };
        BindLive(errorText, TextBlock.TextProperty, nameof(AddEditAppDialogViewModel.RepoFetchError));
        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AddEditAppDialogViewModel.RepoFetchError))
                errorText.Visibility = string.IsNullOrWhiteSpace(_viewModel.RepoFetchError)
                    ? Visibility.Collapsed : Visibility.Visible;
        };
        panel.Children.Add(errorText);

        // ── 3. GitHub repo card (collapsible, appears after fetch) ──
        var repoCard = new Border
        {
            Background = (Brush)App.Current.Resources["CardBackgroundBrush"],
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(16),
            Visibility = Visibility.Collapsed,
            BorderThickness = new Thickness(1),
            BorderBrush = (Brush)App.Current.Resources["BorderDefaultBrush"],
        };
        var repoCardPanel = new StackPanel { Spacing = 12 };
        repoCard.Child = repoCardPanel;

        // Avatar + name + open button
        var repoHeader = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 14 };
        var avatarBorder = new Border
        {
            Width = 56, Height = 56,
            CornerRadius = new CornerRadius(10),
            Background = (Brush)App.Current.Resources["AccentBrush"],
        };
        var avatarImage = new Image { Width = 56, Height = 56, Stretch = Stretch.UniformToFill };
        avatarBorder.Child = avatarImage;
        var avatarBinding = new Binding
        {
            Source = _viewModel,
            Path = new PropertyPath(nameof(AddEditAppDialogViewModel.RepoAvatarUrl)),
            Mode = BindingMode.OneWay,
            Converter = new StringToImageSourceConverter(),
        };
        BindingOperations.SetBinding(avatarImage, Image.SourceProperty, avatarBinding);
        repoHeader.Children.Add(avatarBorder);

        var nameStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 2 };
        var repoTitle = new TextBlock
        {
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            FontSize = 16,
            Foreground = (Brush)App.Current.Resources["TextPrimaryBrush"],
        };
        BindLive(repoTitle, TextBlock.TextProperty, nameof(AddEditAppDialogViewModel.Name));
        nameStack.Children.Add(repoTitle);

        var openRepoBtn = new Button
        {
            Content = AppWatch.Localization.Loc.Instance["OpenInGitHub"],
            Padding = new Thickness(12, 4, 12, 4),
            CornerRadius = new CornerRadius(4),
            Margin = new Thickness(0, 4, 0, 0),
            Style = (Style)App.Current.Resources["AccentButtonStyle"],
        };
        openRepoBtn.Click += (_, _) => _viewModel.OpenRepoInBrowser();
        nameStack.Children.Add(openRepoBtn);
        repoHeader.Children.Add(nameStack);
        repoCardPanel.Children.Add(repoHeader);

        // Description
        var descText = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.7,
            FontSize = 13,
            LineHeight = 20,
        };
        BindLive(descText, TextBlock.TextProperty, nameof(AddEditAppDialogViewModel.RepoDescription));
        repoCardPanel.Children.Add(descText);

        // Homepage + detected kind/exe
        var metaPanel = new StackPanel { Spacing = 6, Orientation = Orientation.Horizontal };

        var homepagePanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        homepagePanel.Children.Add(new FontIcon { Glyph = "\uE774", FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Opacity = 0.6 });
        var homepageLink = new TextBlock { FontSize = 12, Opacity = 0.7, VerticalAlignment = VerticalAlignment.Center };
        BindLive(homepageLink, TextBlock.TextProperty, nameof(AddEditAppDialogViewModel.RepoHomepage));
        homepagePanel.Children.Add(homepageLink);
        metaPanel.Children.Add(homepagePanel);

        var detectedText = new TextBlock
        {
            FontSize = 12,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = (Brush)App.Current.Resources["AccentBrush"],
            VerticalAlignment = VerticalAlignment.Center,
        };
        BindLive(detectedText, TextBlock.TextProperty, nameof(AddEditAppDialogViewModel.DetectedText));
        metaPanel.Children.Add(detectedText);
        repoCardPanel.Children.Add(metaPanel);

        // Release Asset Selector
        var assetPanel = new StackPanel { Spacing = 6, Margin = new Thickness(0, 4, 0, 0), Visibility = Visibility.Collapsed };
        assetPanel.Children.Add(new TextBlock
        {
            Text = AppWatch.Localization.Loc.Instance["ReleaseAsset"],
            FontSize = 12,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = (Brush)App.Current.Resources["TextSecondaryBrush"]
        });
        var assetCombo = new ComboBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ItemsSource = _viewModel.AvailableAssets,
        };
        assetCombo.SelectionChanged += (_, _) =>
        {
            if (assetCombo.SelectedIndex >= 0)
                _viewModel.SelectedAssetIndex = assetCombo.SelectedIndex;
        };
        assetPanel.Children.Add(assetCombo);
        repoCardPanel.Children.Add(assetPanel);

        // Card visibility
        _viewModel.PropertyChanged += (_, e) =>
        {
            switch (e.PropertyName)
            {
                case nameof(AddEditAppDialogViewModel.ShowGitHubInfo):
                    repoCard.Visibility = _viewModel.ShowGitHubInfo ? Visibility.Visible : Visibility.Collapsed;
                    break;
                case nameof(AddEditAppDialogViewModel.RepoHomepage):
                    homepagePanel.Visibility = string.IsNullOrWhiteSpace(_viewModel.RepoHomepage)
                        ? Visibility.Collapsed : Visibility.Visible;
                    break;
                case nameof(AddEditAppDialogViewModel.RepoAvatarUrl):
                    repoCard.Visibility = _viewModel.ShowGitHubInfo ? Visibility.Visible : Visibility.Collapsed;
                    break;
                case nameof(AddEditAppDialogViewModel.HasAvailableAssets):
                    assetPanel.Visibility = _viewModel.HasAvailableAssets ? Visibility.Visible : Visibility.Collapsed;
                    if (_viewModel.SelectedAssetIndex >= 0 && _viewModel.SelectedAssetIndex < assetCombo.Items.Count)
                        assetCombo.SelectedIndex = _viewModel.SelectedAssetIndex;
                    break;
                case nameof(AddEditAppDialogViewModel.SelectedAssetIndex):
                    if (_viewModel.SelectedAssetIndex >= 0 && _viewModel.SelectedAssetIndex < assetCombo.Items.Count && assetCombo.SelectedIndex != _viewModel.SelectedAssetIndex)
                        assetCombo.SelectedIndex = _viewModel.SelectedAssetIndex;
                    break;
            }
        };
        panel.Children.Add(repoCard);

        // ── 4. Essentials ──
        var nameBox = TextBox("Name", AppWatch.Localization.Loc.Instance["AppNamePlaceholder"]);
        panel.Children.Add(Field(AppWatch.Localization.Loc.Instance["AppName"], nameBox));

        // UpdateKind combo
        var kindCombo = new ComboBox { MinWidth = 320, SelectedIndex = _viewModel.SelectedUpdateKindIndex };
        kindCombo.Items.Add(new ComboBoxItem { Content = AppWatch.Localization.Loc.Instance["UpdateKindPortableZip"] });
        kindCombo.Items.Add(new ComboBoxItem { Content = AppWatch.Localization.Loc.Instance["UpdateKindPortableExe"] });
        kindCombo.Items.Add(new ComboBoxItem { Content = AppWatch.Localization.Loc.Instance["UpdateKindInstaller"] });
        kindCombo.Items.Add(new ComboBoxItem { Content = AppWatch.Localization.Loc.Instance["UpdateKindWingetPackage"] });
        kindCombo.Items.Add(new ComboBoxItem { Content = AppWatch.Localization.Loc.Instance["UpdateKindExternal"] });
        kindCombo.SelectionChanged += (_, _) =>
        {
            _viewModel.SelectedUpdateKindIndex = kindCombo.SelectedIndex;
            UpdateVisibility(kindCombo.SelectedIndex);
        };
        panel.Children.Add(Field(AppWatch.Localization.Loc.Instance["UpdateKind"], kindCombo));

        // Dynamic fields based on UpdateKind
        var exeBox = TextBox("ExecutableName", "app.exe");
        _exePanel = Field(AppWatch.Localization.Loc.Instance["ExecutableName"], exeBox);
        panel.Children.Add(_exePanel);

        // ProcessName + Select from running processes button
        var procGrid = new Grid { ColumnSpacing = 8 };
        procGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        procGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var procBox = TextBox("ProcessName", "process");
        _procBox = procBox;
        Grid.SetColumn(procBox, 0);
        procGrid.Children.Add(procBox);

        var selectProcBtn = new Button
        {
            Content = "\uE71D",
            Padding = new Thickness(12, 6, 12, 6),
            CornerRadius = new CornerRadius(6),
        };
        ToolTipService.SetToolTip(selectProcBtn, AppWatch.Localization.Loc.Instance["SelectRunningProcess"]);
        var procFlyout = new MenuFlyout();
        selectProcBtn.Click += (_, _) =>
        {
            procFlyout.Items.Clear();
            var list = _viewModel.GetRunningProcesses();
            if (list.Count == 0)
            {
                procFlyout.Items.Add(new MenuFlyoutItem { Text = AppWatch.Localization.Loc.Instance["NoRunningProcesses"], IsEnabled = false });
            }
            else
            {
                foreach (var p in list)
                {
                    var item = new MenuFlyoutItem { Text = p };
                    item.Click += (_, _) =>
                    {
                        _viewModel.ProcessName = p;
                        procBox.Text = p;
                    };
                    procFlyout.Items.Add(item);
                }
            }
            procFlyout.ShowAt(selectProcBtn);
        };
        Grid.SetColumn(selectProcBtn, 1);
        procGrid.Children.Add(selectProcBtn);
        _procPanel = Field(AppWatch.Localization.Loc.Instance["ProcessName"], procGrid);
        panel.Children.Add(_procPanel);

        // Install directory + browse
        var dirGrid = new Grid { ColumnSpacing = 8 };
        dirGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        dirGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var dirBox = TextBox("InstallDirectory", AppWatch.Localization.Loc.Instance["InstallDirectoryPlaceholder"]);
        _dirBox = dirBox;
        Grid.SetColumn(dirBox, 0);
        dirGrid.Children.Add(dirBox);
        var browseBtn = new Button
        {
            Content = "\uE774",
            Padding = new Thickness(12, 6, 12, 6),
            CornerRadius = new CornerRadius(6),
        };
        ToolTipService.SetToolTip(browseBtn, AppWatch.Localization.Loc.Instance["Browse"]);
        browseBtn.Click += async (_, _) =>
        {
            var picker = new Windows.Storage.Pickers.FolderPicker();
            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.ComputerFolder;
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
            var folder = await picker.PickSingleFolderAsync();
            if (folder is not null)
            {
                _viewModel.InstallDirectory = folder.Path;
                dirBox.Text = folder.Path;
            }
        };
        Grid.SetColumn(browseBtn, 1);
        dirGrid.Children.Add(browseBtn);
        panel.Children.Add(Field(AppWatch.Localization.Loc.Instance["InstallDirectory"], dirGrid));

        // Winget package ID + interactive search
        var wingetContainer = new StackPanel { Spacing = 8 };
        var wingetGrid = new Grid { ColumnSpacing = 8 };
        wingetGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        wingetGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var wingetBox = TextBox("WingetPackageId", AppWatch.Localization.Loc.Instance["WingetSearchPlaceholder"]);
        Grid.SetColumn(wingetBox, 0);
        wingetGrid.Children.Add(wingetBox);

        var wingetSearchStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var wingetSearchBtn = new Button
        {
            Content = AppWatch.Localization.Loc.Instance["WingetSearch"],
            Padding = new Thickness(12, 6, 12, 6),
            CornerRadius = new CornerRadius(6),
            Style = (Style)App.Current.Resources["AccentButtonStyle"],
        };
        var wingetRing = new ProgressRing
        {
            Width = 16, Height = 16,
            IsActive = false,
            Visibility = Visibility.Collapsed,
            VerticalAlignment = VerticalAlignment.Center,
        };
        wingetSearchStack.Children.Add(wingetSearchBtn);
        wingetSearchStack.Children.Add(wingetRing);
        Grid.SetColumn(wingetSearchStack, 1);
        wingetGrid.Children.Add(wingetSearchStack);
        wingetContainer.Children.Add(wingetGrid);

        var wingetStatusText = new TextBlock
        {
            FontSize = 12,
            Foreground = (Brush)App.Current.Resources["TextSecondaryBrush"],
            Visibility = Visibility.Collapsed,
        };
        BindLive(wingetStatusText, TextBlock.TextProperty, nameof(AddEditAppDialogViewModel.WingetSearchStatus));
        wingetContainer.Children.Add(wingetStatusText);

        var wingetResultsList = new StackPanel { Spacing = 4, Visibility = Visibility.Collapsed };
        var wingetScroll = new ScrollViewer { MaxHeight = 160, Content = wingetResultsList };
        wingetContainer.Children.Add(wingetScroll);

        wingetSearchBtn.Click += async (_, _) => await _viewModel.SearchWingetPackageAsync();

        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AddEditAppDialogViewModel.IsSearchingWinget))
            {
                wingetSearchBtn.IsEnabled = !_viewModel.IsSearchingWinget;
                wingetRing.IsActive = _viewModel.IsSearchingWinget;
                wingetRing.Visibility = _viewModel.IsSearchingWinget ? Visibility.Visible : Visibility.Collapsed;
            }
            else if (e.PropertyName == nameof(AddEditAppDialogViewModel.WingetSearchStatus))
            {
                wingetStatusText.Visibility = !string.IsNullOrWhiteSpace(_viewModel.WingetSearchStatus)
                    ? Visibility.Visible : Visibility.Collapsed;
            }
            else if (e.PropertyName == nameof(AddEditAppDialogViewModel.HasWingetSearchResults))
            {
                wingetResultsList.Children.Clear();
                if (_viewModel.HasWingetSearchResults)
                {
                    wingetResultsList.Visibility = Visibility.Visible;
                    foreach (var pkg in _viewModel.WingetSearchResults)
                    {
                        var btn = new Button
                        {
                            HorizontalAlignment = HorizontalAlignment.Stretch,
                            HorizontalContentAlignment = HorizontalAlignment.Left,
                            Padding = new Thickness(10, 6, 10, 6),
                            CornerRadius = new CornerRadius(6),
                            Background = (Brush)App.Current.Resources["CardBackgroundBrush"],
                            BorderThickness = new Thickness(1),
                            Content = new StackPanel
                            {
                                Orientation = Orientation.Horizontal,
                                Spacing = 10,
                                Children =
                                {
                                    new TextBlock { Text = pkg.Name, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontSize = 13 },
                                    new TextBlock { Text = pkg.Id, Opacity = 0.6, FontSize = 12, VerticalAlignment = VerticalAlignment.Center },
                                    new TextBlock { Text = $"v{pkg.Version}", Opacity = 0.5, FontSize = 11, VerticalAlignment = VerticalAlignment.Center },
                                }
                            }
                        };
                        var captured = pkg;
                        btn.Click += (_, _) =>
                        {
                            _viewModel.SelectWingetPackage(captured);
                            wingetBox.Text = captured.Id;
                            if (nameBox.Text != _viewModel.Name) nameBox.Text = _viewModel.Name;
                            wingetResultsList.Visibility = Visibility.Collapsed;
                        };
                        wingetResultsList.Children.Add(btn);
                    }
                }
                else
                {
                    wingetResultsList.Visibility = Visibility.Collapsed;
                }
            }
        };

        _wingetPanel = Field(AppWatch.Localization.Loc.Instance["WingetPackageId"], wingetContainer);
        panel.Children.Add(_wingetPanel);

        // ── 5. Options (vertical) ──
        var options = new StackPanel { Spacing = 10, Margin = new Thickness(0, 4, 0, 0) };
        options.Children.Add(Check(AppWatch.Localization.Loc.Instance["LaunchAfterUpdate"],
            v => _viewModel.LaunchAfterUpdate = v, _viewModel.LaunchAfterUpdate));
        options.Children.Add(Check(AppWatch.Localization.Loc.Instance["IgnorePreReleases"],
            v => _viewModel.IgnorePreReleases = v, _viewModel.IgnorePreReleases));
        options.Children.Add(Check(AppWatch.Localization.Loc.Instance["AutoDetectVersion"],
            v => _viewModel.AutoDetectVersion = v, _viewModel.AutoDetectVersion));

        if (_viewModel.IsNewApp)
        {
            options.Children.Add(Check(AppWatch.Localization.Loc.Instance["InstallImmediately"],
                v => _viewModel.InstallImmediately = v, _viewModel.InstallImmediately));
            options.Children.Add(Check(AppWatch.Localization.Loc.Instance["CreateStartShortcut"],
                v => _viewModel.CreateStartShortcut = v, _viewModel.CreateStartShortcut));
            options.Children.Add(Check(AppWatch.Localization.Loc.Instance["CreateDesktopShortcut"],
                v => _viewModel.CreateDesktopShortcut = v, _viewModel.CreateDesktopShortcut));
        }

        panel.Children.Add(options);

        // ── 6. Advanced expander ──
        var advanced = new StackPanel { Spacing = 14 };

        var strategyCombo = new ComboBox { MinWidth = 320, SelectedIndex = _viewModel.SelectedUpdateStrategyIndex };
        strategyCombo.Items.Add(new ComboBoxItem { Content = AppWatch.Localization.Loc.Instance["SettingsUpdateStrategyAuto"] });
        strategyCombo.Items.Add(new ComboBoxItem { Content = AppWatch.Localization.Loc.Instance["SettingsUpdateStrategyBuiltIn"] });
        strategyCombo.Items.Add(new ComboBoxItem { Content = AppWatch.Localization.Loc.Instance["SettingsUpdateStrategySilent"] });
        strategyCombo.Items.Add(new ComboBoxItem { Content = AppWatch.Localization.Loc.Instance["SettingsUpdateStrategyPortable"] });
        strategyCombo.Items.Add(new ComboBoxItem { Content = AppWatch.Localization.Loc.Instance["SettingsUpdateStrategyExternal"] });
        strategyCombo.Items.Add(new ComboBoxItem { Content = AppWatch.Localization.Loc.Instance["SettingsUpdateStrategyManual"] });
        strategyCombo.SelectionChanged += (_, _) =>
            _viewModel.SelectedUpdateStrategyIndex = strategyCombo.SelectedIndex;
        _strategyPanel = Field(AppWatch.Localization.Loc.Instance["UpdateStrategy"], strategyCombo);
        advanced.Children.Add(_strategyPanel);

        var vfPanel = new StackPanel { Spacing = 14 };
        vfPanel.Children.Add(Field(AppWatch.Localization.Loc.Instance["VersionFilePath"], TextBox("VersionFilePath", AppWatch.Localization.Loc.Instance["VersionFilePathPlaceholder"])));
        vfPanel.Children.Add(Field(AppWatch.Localization.Loc.Instance["VersionFileRegex"], TextBox("VersionFileRegex", AppWatch.Localization.Loc.Instance["VersionFileRegexPlaceholder"])));
        _versionFilePanel = vfPanel;
        advanced.Children.Add(_versionFilePanel);

        var unsignedCheck = Check(AppWatch.Localization.Loc.Instance["AllowUnsignedInstaller"],
            v => _viewModel.AllowUnsignedInstaller = v, _viewModel.AllowUnsignedInstaller);
        advanced.Children.Add(unsignedCheck);

        var expander = new Expander
        {
            Header = AppWatch.Localization.Loc.Instance["AdvancedSection"],
            Content = advanced,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            IsExpanded = false,
        };
        panel.Children.Add(expander);

        UpdateVisibility(_viewModel.SelectedUpdateKindIndex);

        // VM → controls sync (after fetch auto-fills)
        _viewModel.PropertyChanged += (_, e) =>
        {
            switch (e.PropertyName)
            {
                case nameof(AddEditAppDialogViewModel.Name):
                    if (nameBox.Text != _viewModel.Name) nameBox.Text = _viewModel.Name;
                    break;
                case nameof(AddEditAppDialogViewModel.ExecutableName):
                    if (exeBox.Text != _viewModel.ExecutableName) exeBox.Text = _viewModel.ExecutableName;
                    break;
                case nameof(AddEditAppDialogViewModel.SelectedUpdateKindIndex):
                    if (kindCombo.SelectedIndex != _viewModel.SelectedUpdateKindIndex)
                        kindCombo.SelectedIndex = _viewModel.SelectedUpdateKindIndex;
                    UpdateVisibility(_viewModel.SelectedUpdateKindIndex);
                    break;
                case nameof(AddEditAppDialogViewModel.InstallDirectory):
                    if (_dirBox is not null && _dirBox.Text != _viewModel.InstallDirectory)
                        _dirBox.Text = _viewModel.InstallDirectory;
                    break;
                case nameof(AddEditAppDialogViewModel.ProcessName):
                    if (_procBox is not null && _procBox.Text != _viewModel.ProcessName)
                        _procBox.Text = _viewModel.ProcessName;
                    break;
                case nameof(AddEditAppDialogViewModel.WingetPackageId):
                    if (wingetBox.Text != _viewModel.WingetPackageId)
                        wingetBox.Text = _viewModel.WingetPackageId;
                    break;
            }
        };

        return new ScrollViewer
        {
            Content = panel,
            MaxHeight = 620,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
    }

    private StackPanel? _strategyPanel;
    private StackPanel? _exePanel;
    private StackPanel? _procPanel;
    private StackPanel? _wingetPanel;
    private StackPanel? _versionFilePanel;
    private TextBox? _dirBox;
    private TextBox? _procBox;

    private void UpdateVisibility(int updateKindIndex)
    {
        var isWinget = updateKindIndex == (int)UpdateKind.WingetPackage;
        var isExternal = updateKindIndex == (int)UpdateKind.External;
        var isInstaller = updateKindIndex == (int)UpdateKind.Installer;
        if (_strategyPanel is not null) _strategyPanel.Visibility = isWinget || isExternal ? Visibility.Collapsed : Visibility.Visible;
        if (_exePanel is not null) _exePanel.Visibility = isWinget ? Visibility.Collapsed : Visibility.Visible;
        if (_procPanel is not null) _procPanel.Visibility = isWinget ? Visibility.Collapsed : Visibility.Visible;
        if (_wingetPanel is not null) _wingetPanel.Visibility = isWinget ? Visibility.Visible : Visibility.Collapsed;
        if (_versionFilePanel is not null) _versionFilePanel.Visibility = isExternal ? Visibility.Visible : Visibility.Collapsed;
        if (_dirBox is not null)
        {
            _dirBox.PlaceholderText = isInstaller
                ? AppWatch.Localization.Loc.Instance["InstallDirectoryInstallerHint"]
                : AppWatch.Localization.Loc.Instance["InstallDirectoryPlaceholder"];
        }
    }

    private void BindLive(FrameworkElement target, DependencyProperty property, string vmProperty)
    {
        var binding = new Binding
        {
            Source = _viewModel,
            Path = new PropertyPath(vmProperty),
            Mode = BindingMode.OneWay,
        };
        BindingOperations.SetBinding(target, property, binding);
    }

    private static StackPanel Field(string label, FrameworkElement input)
    {
        return new StackPanel
        {
            Spacing = 4,
            Children =
            {
                new TextBlock { Text = label, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontSize = 13 },
                input,
            },
        };
    }

    private static CheckBox Check(string label, Action<bool> set, bool initial)
    {
        var box = new CheckBox { Content = label, IsChecked = initial };
        box.Checked += (_, _) => set(true);
        box.Unchecked += (_, _) => set(false);
        return box;
    }

    private TextBox TextBox(string propertyName, string placeholder)
    {
        var box = new TextBox { PlaceholderText = placeholder };
        var prop = typeof(AddEditAppDialogViewModel).GetProperty(propertyName);
        if (prop?.GetValue(_viewModel) is string initial)
            box.Text = initial;
        box.TextChanged += (_, _) => prop?.SetValue(_viewModel, box.Text);
        return box;
    }
}
