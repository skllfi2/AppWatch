using AppWatch.Core.Domain.Models;
using AppWatch.Converters;
using AppWatch.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace AppWatch.Views;

/// <summary>
/// Add/Edit custom app dialog — built in C# to avoid XamlCompiler crashes with ContentDialog on RU locale.
///
/// Layout v3 (simplified):
///   1. Repo input (owner/repo or full URL) + "Fetch from GitHub" button
///   2. Repo card: large avatar, name, description, homepage, "Open in GitHub" button
///   3. Essentials: Name, UpdateKind, (Exe/Process/InstallDir OR WingetId + Search)
///   4. Options: 3 checkboxes vertical
///   5. Advanced expander: strategy, version-file fields, unsigned consent
/// Primary button enabled when Name + (RepoOwner+RepoName for GitHub kinds OR WingetPackageId for winget).
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
        MinWidth = 520;

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
        var panel = new StackPanel { Spacing = 16, Padding = new Thickness(0, 8, 0, 0) };

        // ── 1. Repo input row ──
        var repoGrid = new Grid { ColumnSpacing = 10 };
        repoGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        repoGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var repoBox = new TextBox
        {
            PlaceholderText = AppWatch.Localization.Loc.Instance["RepoInputPlaceholder"],
            Text = _viewModel.RepoInput,
        };
        repoBox.TextChanged += (_, _) => _viewModel.RepoInput = repoBox.Text;
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

        // ── 2. GitHub repo card (collapsible, appears after fetch) ──
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
            }
        };
        panel.Children.Add(repoCard);

        // ── 3. Essentials ──
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

        var procBox = TextBox("ProcessName", "process");
        _procPanel = Field(AppWatch.Localization.Loc.Instance["ProcessName"], procBox);
        panel.Children.Add(_procPanel);

        // Install directory + browse
        var dirGrid = new Grid { ColumnSpacing = 8 };
        dirGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        dirGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var dirBox = TextBox("InstallDirectory", AppWatch.Localization.Loc.Instance["InstallDirectoryPlaceholder"]);
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

        // Winget package ID + search
        var wingetGrid = new Grid { ColumnSpacing = 8 };
        wingetGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        wingetGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var wingetBox = TextBox("WingetPackageId", AppWatch.Localization.Loc.Instance["WingetSearchPlaceholder"]);
        Grid.SetColumn(wingetBox, 0);
        wingetGrid.Children.Add(wingetBox);
        var wingetSearchBtn = new Button
        {
            Content = AppWatch.Localization.Loc.Instance["WingetSearch"],
            Padding = new Thickness(12, 6, 12, 6),
            CornerRadius = new CornerRadius(6),
        };
        wingetSearchBtn.Click += async (_, _) => await _viewModel.SearchWingetPackageAsync();
        Grid.SetColumn(wingetSearchBtn, 1);
        wingetGrid.Children.Add(wingetSearchBtn);
        _wingetPanel = Field(AppWatch.Localization.Loc.Instance["WingetPackageId"], wingetGrid);
        panel.Children.Add(_wingetPanel);

        // ── 4. Options (vertical, no truncation) ──
        var options = new StackPanel { Spacing = 10, Margin = new Thickness(0, 4, 0, 0) };
        options.Children.Add(Check(AppWatch.Localization.Loc.Instance["LaunchAfterUpdate"],
            v => _viewModel.LaunchAfterUpdate = v, _viewModel.LaunchAfterUpdate));
        options.Children.Add(Check(AppWatch.Localization.Loc.Instance["IgnorePreReleases"],
            v => _viewModel.IgnorePreReleases = v, _viewModel.IgnorePreReleases));
        options.Children.Add(Check(AppWatch.Localization.Loc.Instance["AutoDetectVersion"],
            v => _viewModel.AutoDetectVersion = v, _viewModel.AutoDetectVersion));
        panel.Children.Add(options);

        // ── 5. Advanced expander ──
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
                    break;
            }
        };

        return new ScrollViewer
        {
            Content = panel,
            MaxHeight = 600,
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
