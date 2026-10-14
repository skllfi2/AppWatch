using AppWatch.Core.Domain.Models;
using AppWatch.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

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
        panel.Children.Add(Field(loc["RepoOwner"], TextBox("RepoOwner", "owner")));
        panel.Children.Add(Field(loc["RepoName"], TextBox("RepoName", "repo")));

        // Update Kind combo
        var kindCombo = new ComboBox { MinWidth = 320, SelectedIndex = _viewModel.SelectedUpdateKindIndex };
        kindCombo.Items.Add(new ComboBoxItem { Content = loc["UpdateKindPortableZip"] });
        kindCombo.Items.Add(new ComboBoxItem { Content = loc["UpdateKindInstaller"] });
        kindCombo.Items.Add(new ComboBoxItem { Content = loc["UpdateKindWingetPackage"] });
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

    private void UpdateVisibility(int updateKindIndex)
    {
        var isWinget = updateKindIndex == (int)UpdateKind.WingetPackage;
        if (_strategyPanel is not null) _strategyPanel.Visibility = isWinget ? Visibility.Collapsed : Visibility.Visible;
        if (_exePanel is not null) _exePanel.Visibility = isWinget ? Visibility.Collapsed : Visibility.Visible;
        if (_procPanel is not null) _procPanel.Visibility = isWinget ? Visibility.Collapsed : Visibility.Visible;
        if (_wingetPanel is not null) _wingetPanel.Visibility = isWinget ? Visibility.Visible : Visibility.Collapsed;
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