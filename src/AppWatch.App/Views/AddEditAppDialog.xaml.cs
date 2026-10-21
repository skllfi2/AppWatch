using AppWatch.Core.Domain.Models;
using AppWatch.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;

namespace AppWatch.Views;

/// <summary>
/// Add/Edit custom app dialog — built entirely in C# to avoid XamlCompiler
/// crashes with ContentDialog x:Class on Russian locale (WMC9999 masks errors).
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

        Content = BuildContent();
        MinWidth = 480;
    }

    public AddEditAppDialogViewModel ViewModel => _viewModel;

    private ScrollViewer BuildContent()
    {
        var loc = AppWatch.Localization.Loc.Instance;
                var panel = new StackPanel { Spacing = 14, Padding = new Thickness(0, 8, 0, 0) };

                panel.Children.Add(Field(loc["AppName"], TextBox("Name", loc["AppNamePlaceholder"])));

                // Repo owner + name row with fetch button
                var repoGrid = new Grid { ColumnSpacing = 8 };
                repoGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                repoGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                repoGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var ownerBox = TextBox("RepoOwner", "owner");
                Grid.SetColumn(ownerBox, 0);
                repoGrid.Children.Add(ownerBox);

                var nameBox = TextBox("RepoName", "repo");
                Grid.SetColumn(nameBox, 1);
                repoGrid.Children.Add(nameBox);

                var fetchBtn = new Button
                {
                    Content = loc["FetchFromGitHub"],
                    Padding = new Thickness(12, 4, 12, 4),
                    CornerRadius = new CornerRadius(4),
                    VerticalAlignment = VerticalAlignment.Bottom
                };
                fetchBtn.Click += async (_, _) => await _viewModel.FetchRepoInfoAsync();
                Grid.SetColumn(fetchBtn, 2);
                repoGrid.Children.Add(fetchBtn);

                panel.Children.Add(Field(loc["RepoOwner"], repoGrid));

                // GitHub repo info card (shown after fetch)
                var githubInfoCard = new Border
                {
                    Background = (Microsoft.UI.Xaml.Media.Brush)App.Current.Resources["CardBackgroundBrush"],
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(12),
                    Margin = new Thickness(0, 4, 0, 0),
                    Visibility = Visibility.Collapsed
                };
                var githubInfoPanel = new StackPanel { Spacing = 8 };
                githubInfoCard.Child = githubInfoPanel;

                var githubHeader = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
                var avatarImage = new Microsoft.UI.Xaml.Controls.Image
                {
                    Width = 40,
                    Height = 40,
                    Stretch = Microsoft.UI.Xaml.Media.Stretch.UniformToFill
                };
                var avatarBinding = new Binding { Source = _viewModel, Path = new PropertyPath("RepoAvatarUrl"), Mode = BindingMode.OneWay };
                BindingOperations.SetBinding(avatarImage, Microsoft.UI.Xaml.Controls.Image.SourceProperty, avatarBinding);
                githubHeader.Children.Add(avatarImage);
                var githubTitle = new TextBlock { FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontSize = 14, VerticalAlignment = VerticalAlignment.Center };
                var titleBinding = new Binding { Source = _viewModel, Path = new PropertyPath("Name"), Mode = BindingMode.OneWay };
                BindingOperations.SetBinding(githubTitle, TextBlock.TextProperty, titleBinding);
                githubHeader.Children.Add(githubTitle);
                var openRepoBtn = new Button
                {
                    Content = loc["OpenInGitHub"],
                    Padding = new Thickness(10, 4, 10, 4),
                    CornerRadius = new CornerRadius(4),
                    Margin = new Thickness(8, 0, 0, 0)
                };
                openRepoBtn.Click += (_, _) => _viewModel.OpenRepoInBrowser();
                githubHeader.Children.Add(openRepoBtn);
                githubInfoPanel.Children.Add(githubHeader);

                if (!string.IsNullOrEmpty(_viewModel.RepoDescription))
                {
                    var descText = new TextBlock
                    {
                        Text = _viewModel.RepoDescription,
                        TextWrapping = TextWrapping.Wrap,
                        Opacity = 0.7,
                        FontSize = 12
                    };
                    githubInfoPanel.Children.Add(descText);
                }

                var homepagePanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Visibility = Visibility.Collapsed };
                var homepageIcon = new FontIcon { Glyph = "\uE774", FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
                homepagePanel.Children.Add(homepageIcon);
                var homepageLink = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
                var homepageBinding = new Binding { Source = _viewModel, Path = new PropertyPath("RepoHomepage"), Mode = BindingMode.OneWay };
                BindingOperations.SetBinding(homepageLink, TextBlock.TextProperty, homepageBinding);
                homepagePanel.Children.Add(homepageLink);
                githubInfoPanel.Children.Add(homepagePanel);

                // Wire visibility for GitHub info card
                _viewModel.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(AddEditAppDialogViewModel.ShowGitHubInfo))
                    {
                        githubInfoCard.Visibility = _viewModel.ShowGitHubInfo ? Visibility.Visible : Visibility.Collapsed;
                    }
                    else if (e.PropertyName == nameof(AddEditAppDialogViewModel.RepoHomepage))
                    {
                        homepagePanel.Visibility = string.IsNullOrWhiteSpace(_viewModel.RepoHomepage) ? Visibility.Collapsed : Visibility.Visible;
                    }
                };

                panel.Children.Add(githubInfoCard);

        // Update Kind combo — items follow the UpdateKind enum order 1:1
        // (fixes pre-existing bug: winget label at index 2 saved as Installer enum)
        var kindCombo = new ComboBox { MinWidth = 320, SelectedIndex = _viewModel.SelectedUpdateKindIndex };
        kindCombo.Items.Add(new ComboBoxItem { Content = loc["UpdateKindPortableZip"] });
        kindCombo.Items.Add(new ComboBoxItem { Content = loc["UpdateKindPortableExe"] });
        kindCombo.Items.Add(new ComboBoxItem { Content = loc["UpdateKindInstaller"] });
        kindCombo.Items.Add(new ComboBoxItem { Content = loc["UpdateKindWingetPackage"] });
        kindCombo.Items.Add(new ComboBoxItem { Content = loc["UpdateKindExternal"] });
        kindCombo.SelectionChanged += (_, _) =>
        {
            _viewModel.SelectedUpdateKindIndex = kindCombo.SelectedIndex;
            UpdateVisibility(kindCombo.SelectedIndex);
        };
        panel.Children.Add(Field(loc["UpdateKind"], kindCombo));

        // Update Strategy combo (hidden for winget)
        var strategyCombo = new ComboBox { MinWidth = 320, SelectedIndex = _viewModel.SelectedUpdateStrategyIndex };
        strategyCombo.Items.Add(new ComboBoxItem { Content = loc["SettingsUpdateStrategyAuto"] });
        strategyCombo.Items.Add(new ComboBoxItem { Content = loc["SettingsUpdateStrategyBuiltIn"] });
        strategyCombo.Items.Add(new ComboBoxItem { Content = loc["SettingsUpdateStrategySilent"] });
        strategyCombo.Items.Add(new ComboBoxItem { Content = loc["SettingsUpdateStrategyPortable"] });
        strategyCombo.Items.Add(new ComboBoxItem { Content = loc["SettingsUpdateStrategyExternal"] });
        strategyCombo.Items.Add(new ComboBoxItem { Content = loc["SettingsUpdateStrategyManual"] });
        strategyCombo.SelectionChanged += (_, _) =>
            _viewModel.SelectedUpdateStrategyIndex = strategyCombo.SelectedIndex;
        _strategyPanel = Field(loc["UpdateStrategy"], strategyCombo);
        panel.Children.Add(_strategyPanel);

        // Executable name (hidden for winget)
        _exePanel = Field(loc["ExecutableName"], TextBox("ExecutableName", "app.exe"));
        panel.Children.Add(_exePanel);

        // Process name (hidden for winget)
        _procPanel = Field(loc["ProcessName"], TextBox("ProcessName", "process"));
        panel.Children.Add(_procPanel);

        // Winget package id (visible only for winget)
        _wingetPanel = Field(loc["WingetPackageId"], TextBox("WingetPackageId", "Microsoft.PowerToys"));
        panel.Children.Add(_wingetPanel);

        // P3: version file fields (visible only for External kind — Battle.net case)
        _versionFilePanel = new StackPanel { Spacing = 14 };
        _versionFilePanel.Children.Add(Field(loc["VersionFilePath"], TextBox("VersionFilePath", loc["VersionFilePathPlaceholder"])));
        _versionFilePanel.Children.Add(Field(loc["VersionFileRegex"], TextBox("VersionFileRegex", loc["VersionFileRegexPlaceholder"])));
        panel.Children.Add(_versionFilePanel);

        // Install directory
        var dirGrid = new Grid { ColumnSpacing = 8 };
        dirGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        dirGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var dirBox = TextBox("InstallDirectory", loc["InstallDirectoryPlaceholder"]);
        Grid.SetColumn(dirBox, 0);
        dirGrid.Children.Add(dirBox);
        var browseBtn = new Button { Content = "...", Padding = new Thickness(8, 4, 8, 4), CornerRadius = new CornerRadius(4) };
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
        panel.Children.Add(Field(loc["InstallDirectory"], dirGrid));

        // Options row
        var options = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 14, Margin = new Thickness(0, 8, 0, 0) };
        var launchCheck = new CheckBox { Content = loc["LaunchAfterUpdate"], IsChecked = _viewModel.LaunchAfterUpdate };
        launchCheck.Checked += (_, _) => _viewModel.LaunchAfterUpdate = true;
        launchCheck.Unchecked += (_, _) => _viewModel.LaunchAfterUpdate = false;
        options.Children.Add(launchCheck);

        var preRelCheck = new CheckBox { Content = loc["IgnorePreReleases"], IsChecked = _viewModel.IgnorePreReleases };
        preRelCheck.Checked += (_, _) => _viewModel.IgnorePreReleases = true;
        preRelCheck.Unchecked += (_, _) => _viewModel.IgnorePreReleases = false;
        options.Children.Add(preRelCheck);

        var autoDetectCheck = new CheckBox { Content = loc["AutoDetectVersion"], IsChecked = _viewModel.AutoDetectVersion };
        autoDetectCheck.Checked += (_, _) => _viewModel.AutoDetectVersion = true;
        autoDetectCheck.Unchecked += (_, _) => _viewModel.AutoDetectVersion = false;
        options.Children.Add(autoDetectCheck);
        panel.Children.Add(options);

        // B7: per-app consent for unsigned installers (separate row — security-relevant)
        var unsignedCheck = new CheckBox
        {
            Content = loc["AllowUnsignedInstaller"],
            IsChecked = _viewModel.AllowUnsignedInstaller,
            Margin = new Thickness(0, 4, 0, 0),
        };
        unsignedCheck.Checked += (_, _) => _viewModel.AllowUnsignedInstaller = true;
        unsignedCheck.Unchecked += (_, _) => _viewModel.AllowUnsignedInstaller = false;
        panel.Children.Add(unsignedCheck);

        UpdateVisibility(_viewModel.SelectedUpdateKindIndex);

        return new ScrollViewer
        {
            Content = panel,
            MaxHeight = 520,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
    }

    private StackPanel? _strategyPanel;
    private StackPanel? _exePanel;
    private StackPanel? _procPanel;
    private StackPanel? _wingetPanel;
    private StackPanel? _versionFilePanel;

    private void UpdateVisibility(int updateKindIndex)
    {
        var isWinget = updateKindIndex == (int)UpdateKind.WingetPackage;
        var isExternal = updateKindIndex == (int)UpdateKind.External;
        if (_strategyPanel is not null) _strategyPanel.Visibility = isWinget || isExternal ? Visibility.Collapsed : Visibility.Visible;
        if (_exePanel is not null) _exePanel.Visibility = isWinget ? Visibility.Collapsed : Visibility.Visible;
        if (_procPanel is not null) _procPanel.Visibility = isWinget ? Visibility.Collapsed : Visibility.Visible;
        if (_wingetPanel is not null) _wingetPanel.Visibility = isWinget ? Visibility.Visible : Visibility.Collapsed;
        if (_versionFilePanel is not null) _versionFilePanel.Visibility = isExternal ? Visibility.Visible : Visibility.Collapsed;
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