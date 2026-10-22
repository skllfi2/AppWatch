using AppWatch.Core.Domain.Models;
using AppWatch.Converters;
using AppWatch.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace AppWatch.Views;

/// <summary>
/// Add/Edit custom app dialog — built entirely in C# to avoid XamlCompiler
/// crashes with ContentDialog x:Class on Russian locale (WMC9999 masks errors).
///
/// Layout v2 (progressive disclosure):
///   1. Single repo input (owner/repo OR full github.com URL) + fetch button
///   2. Live repo card: avatar, name, description, homepage, detected kind/exe
///   3. Essentials: app name, update kind, exe name, install dir
///   4. Checkboxes vertical (no truncation at 480px)
///   5. «Дополнительно» expander: strategy, process name, winget id,
///      version-file fields, unsigned-installer consent
/// Primary button is gated by VM.CanAdd.
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

        // Gate the primary button: disabled until name + repo (or winget id) present
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
        var loc = AppWatch.Localization.Loc.Instance;
        var panel = new StackPanel { Spacing = 14, Padding = new Thickness(0, 8, 0, 0) };

        // ── 1. Repo input: accepts owner/repo, github.com/owner/repo, full URL ──
        var repoGrid = new Grid { ColumnSpacing = 8 };
        repoGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        repoGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var repoBox = new TextBox { PlaceholderText = loc["RepoInputPlaceholder"] };
        repoBox.Text = _viewModel.RepoInput;
        repoBox.TextChanged += (_, _) => _viewModel.RepoInput = repoBox.Text;
        Grid.SetColumn(repoBox, 0);
        repoGrid.Children.Add(repoBox);

        var fetchStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        var fetchBtn = new Button
        {
            Content = loc["FetchFromGitHub"],
            Padding = new Thickness(12, 4, 12, 4),
            CornerRadius = new CornerRadius(4),
        };
        var fetchRing = new ProgressRing
        {
            Width = 14, Height = 14,
            IsActive = false,
            Visibility = Visibility.Collapsed,
            VerticalAlignment = VerticalAlignment.Center,
        };
        fetchStack.Children.Add(fetchBtn);
        fetchStack.Children.Add(fetchRing);
        fetchBtn.Click += async (_, _) => await _viewModel.FetchRepoInfoAsync();
        Grid.SetColumn(fetchStack, 1);
        repoGrid.Children.Add(fetchStack);

        panel.Children.Add(Field(loc["RepoInputLabel"], repoGrid));

        // Fetch state: button busy + ring visible
        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AddEditAppDialogViewModel.IsFetchingRepoInfo))
            {
                fetchBtn.IsEnabled = !_viewModel.IsFetchingRepoInfo;
                fetchRing.IsActive = _viewModel.IsFetchingRepoInfo;
                fetchRing.Visibility = _viewModel.IsFetchingRepoInfo ? Visibility.Visible : Visibility.Collapsed;
            }
        };

        // Fetch error (red, live-updating)
        var errorText = new TextBlock
        {
            Foreground = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 0xD8, 0x3B, 0x01)),
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

        // ── 2. GitHub repo card (live bindings) ──
        var repoCard = new Border
        {
            Background = (Brush)App.Current.Resources["CardBackgroundBrush"],
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12),
            Visibility = Visibility.Collapsed,
        };
        var repoCardPanel = new StackPanel { Spacing = 8 };
        repoCard.Child = repoCardPanel;

        var repoHeader = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        var avatarImage = new Image { Width = 40, Height = 40, Stretch = Stretch.UniformToFill };
        var avatarBinding = new Binding
        {
            Source = _viewModel,
            Path = new PropertyPath(nameof(AddEditAppDialogViewModel.RepoAvatarUrl)),
            Mode = BindingMode.OneWay,
            Converter = new StringToImageSourceConverter(),
        };
        BindingOperations.SetBinding(avatarImage, Image.SourceProperty, avatarBinding);
        repoHeader.Children.Add(avatarImage);

        var repoTitle = new TextBlock
        {
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            FontSize = 14,
            VerticalAlignment = VerticalAlignment.Center,
        };
        BindLive(repoTitle, TextBlock.TextProperty, nameof(AddEditAppDialogViewModel.Name));
        repoHeader.Children.Add(repoTitle);

        var openRepoBtn = new Button
        {
            Content = loc["OpenInGitHub"],
            Padding = new Thickness(10, 4, 10, 4),
            CornerRadius = new CornerRadius(4),
            Margin = new Thickness(8, 0, 0, 0),
        };
        openRepoBtn.Click += (_, _) => _viewModel.OpenRepoInBrowser();
        repoHeader.Children.Add(openRepoBtn);
        repoCardPanel.Children.Add(repoHeader);

        var descText = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.7,
            FontSize = 12,
        };
        BindLive(descText, TextBlock.TextProperty, nameof(AddEditAppDialogViewModel.RepoDescription));
        repoCardPanel.Children.Add(descText);

        var homepagePanel = new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 8,
            Visibility = Visibility.Collapsed,
        };
        homepagePanel.Children.Add(new FontIcon { Glyph = "\uE774", FontSize = 12, VerticalAlignment = VerticalAlignment.Center });
        var homepageLink = new TextBlock { VerticalAlignment = VerticalAlignment.Center, FontSize = 12, Opacity = 0.7 };
        BindLive(homepageLink, TextBlock.TextProperty, nameof(AddEditAppDialogViewModel.RepoHomepage));
        homepagePanel.Children.Add(homepageLink);
        repoCardPanel.Children.Add(homepagePanel);

        // Detected kind/exe line (accent)
        var detectedText = new TextBlock
        {
            FontSize = 12,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = (Brush)App.Current.Resources["AccentBrush"],
        };
        BindLive(detectedText, TextBlock.TextProperty, nameof(AddEditAppDialogViewModel.DetectedText));
        repoCardPanel.Children.Add(detectedText);

        // Card + homepage visibility react to VM changes
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
        var nameBox = TextBox("Name", loc["AppNamePlaceholder"]);
        panel.Children.Add(Field(loc["AppName"], nameBox));

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

        var exeBox = TextBox("ExecutableName", "app.exe");
        _exePanel = Field(loc["ExecutableName"], exeBox);
        panel.Children.Add(_exePanel);

        // Install directory + browse
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

        // ── 4. Options: vertical (row layout truncated the third label at 480px) ──
        var options = new StackPanel { Spacing = 8, Margin = new Thickness(0, 4, 0, 0) };
        options.Children.Add(Check(loc["LaunchAfterUpdate"],
            v => _viewModel.LaunchAfterUpdate = v, _viewModel.LaunchAfterUpdate));
        options.Children.Add(Check(loc["IgnorePreReleases"],
            v => _viewModel.IgnorePreReleases = v, _viewModel.IgnorePreReleases));
        options.Children.Add(Check(loc["AutoDetectVersion"],
            v => _viewModel.AutoDetectVersion = v, _viewModel.AutoDetectVersion));
        panel.Children.Add(options);

        // ── 5. «Дополнительно» expander: rare fields out of the way ──
        var advanced = new StackPanel { Spacing = 14 };

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
        advanced.Children.Add(_strategyPanel);

        _procPanel = Field(loc["ProcessName"], TextBox("ProcessName", "process"));
        advanced.Children.Add(_procPanel);

        _wingetPanel = Field(loc["WingetPackageId"], TextBox("WingetPackageId", "Microsoft.PowerToys"));
        advanced.Children.Add(_wingetPanel);

        _versionFilePanel = new StackPanel { Spacing = 14 };
        _versionFilePanel.Children.Add(Field(loc["VersionFilePath"], TextBox("VersionFilePath", loc["VersionFilePathPlaceholder"])));
        _versionFilePanel.Children.Add(Field(loc["VersionFileRegex"], TextBox("VersionFileRegex", loc["VersionFileRegexPlaceholder"])));
        advanced.Children.Add(_versionFilePanel);

        var unsignedCheck = Check(loc["AllowUnsignedInstaller"],
            v => _viewModel.AllowUnsignedInstaller = v, _viewModel.AllowUnsignedInstaller);
        advanced.Children.Add(unsignedCheck);

        var expander = new Expander
        {
            Header = loc["AdvancedSection"],
            Content = advanced,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            IsExpanded = _viewModel.IsNewApp is false && _viewModel.SelectedUpdateKindIndex == (int)UpdateKind.WingetPackage,
        };
        panel.Children.Add(expander);

        UpdateVisibility(_viewModel.SelectedUpdateKindIndex);

        // VM → controls sync: fetch auto-fills Name/ExecutableName/Kind in the VM,
        // but these boxes were built before the fetch (one-way box→VM only).
        _viewModel.PropertyChanged += (_, e) =>
        {
            switch (e.PropertyName)
            {
                case nameof(AddEditAppDialogViewModel.Name):
                    if (nameBox.Text != _viewModel.Name)
                        nameBox.Text = _viewModel.Name;
                    break;
                case nameof(AddEditAppDialogViewModel.ExecutableName):
                    if (exeBox.Text != _viewModel.ExecutableName)
                        exeBox.Text = _viewModel.ExecutableName;
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
            MaxHeight = 560,
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

    /// <summary>Live OneWay binding to a VM property (classic {Binding}: no x:Bind in runtime-built trees).</summary>
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
