using AppWatch.Catalog;
using AppWatch.Core.Abstractions;
using AppWatch.Core.Domain;
using AppWatch.Core.Domain.Models;
using AppWatch.Infrastructure;
using AppWatch.ViewModels;
using AppWatch.Views;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AppWatch.Services;

public sealed class DialogService : IDialogService
{
    private readonly SystemVersionResolver _systemVersion;
    private readonly CatalogService _catalog;
    private readonly IConfigStore _configStore;

    public DialogService(
        SystemVersionResolver systemVersion,
        CatalogService catalog,
        IConfigStore configStore)
    {
        _systemVersion = systemVersion;
        _catalog = catalog;
        _configStore = configStore;
    }

    private static XamlRoot GetXamlRoot()
    {
        return App.MainWindow?.Content?.XamlRoot
            ?? throw new InvalidOperationException("Active XamlRoot is not available.");
    }

    public async Task<(bool Confirmed, TrackedApp? App, bool InstallImmediately, bool CreateStartShortcut, bool CreateDesktopShortcut)> ShowAddEditAppDialogAsync(TrackedApp? app = null, string? prefillUrlOrFile = null)
    {
        var dialog = new AddEditAppDialog(app)
        {
            XamlRoot = GetXamlRoot()
        };

        if (!string.IsNullOrWhiteSpace(prefillUrlOrFile))
        {
            if (File.Exists(prefillUrlOrFile))
            {
                await dialog.ViewModel.ApplyLocalFileAsync(prefillUrlOrFile);
            }
            else
            {
                dialog.ViewModel.RepoInput = prefillUrlOrFile;
                _ = dialog.ViewModel.FetchRepoInfoAsync();
            }
        }

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            var tracked = dialog.ViewModel.ToTrackedApp();
            return (true, tracked, dialog.ViewModel.InstallImmediately, dialog.ViewModel.CreateStartShortcut, dialog.ViewModel.CreateDesktopShortcut);
        }

        return (false, null, false, false, false);
    }

    public async Task<IReadOnlyList<TrackedApp>?> ShowSystemScannerDialogAsync()
    {
        var config = _configStore.Load();
        var dialog = new SystemScannerDialog(_systemVersion, _catalog, config.TrackedApps)
        {
            XamlRoot = GetXamlRoot()
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            return dialog.ViewModel.CreateTrackedApps();
        }

        return null;
    }

    public async Task<IReadOnlyList<TrackedApp>?> ShowBatchImportDialogAsync(string? initialText = null)
    {
        var config = _configStore.Load();
        var dialog = new BatchImportDialog(config.TrackedApps, _catalog, initialText)
        {
            XamlRoot = GetXamlRoot()
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            return dialog.ViewModel.CreateTrackedApps();
        }

        return null;
    }

    public async Task<bool> ShowConfirmAsync(string title, string message, string primaryText = "Да", string closeText = "Отмена")
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            PrimaryButtonText = primaryText,
            CloseButtonText = closeText,
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = GetXamlRoot()
        };

        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary;
    }

    public async Task ShowMessageAsync(string title, string message, string closeText = "ОК")
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            CloseButtonText = closeText,
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = GetXamlRoot()
        };

        await dialog.ShowAsync();
    }

    public async Task<VersionArchiveEntry?> ShowRollbackDialogAsync(TrackedApp app)
    {
        var dialog = new RollbackVersionDialog(app)
        {
            XamlRoot = GetXamlRoot()
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary && dialog.SelectedEntry is not null)
        {
            return dialog.SelectedEntry;
        }

        return null;
    }

    public async Task ShowAppDetailsDialogAsync(TrackedAppItem item, IReadOnlyList<UpdateHistoryEntry> history, Action<bool> onConsentChanged)
    {
        var dialog = new AppDetailsDialog(item, history, onConsentChanged)
        {
            XamlRoot = GetXamlRoot()
        };

        await dialog.ShowAsync();
    }
}
