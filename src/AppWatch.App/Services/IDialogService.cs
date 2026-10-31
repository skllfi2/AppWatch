using AppWatch.Core.Domain;
using AppWatch.Core.Domain.Models;
using AppWatch.ViewModels;

namespace AppWatch.Services;

public interface IDialogService
{
    Task<(bool Confirmed, TrackedApp? App, bool InstallImmediately, bool CreateStartShortcut, bool CreateDesktopShortcut)> ShowAddEditAppDialogAsync(TrackedApp? app = null, string? prefillUrlOrFile = null);
    Task<IReadOnlyList<TrackedApp>?> ShowSystemScannerDialogAsync();
    Task<IReadOnlyList<TrackedApp>?> ShowBatchImportDialogAsync(string? initialText = null);
    Task<bool> ShowConfirmAsync(string title, string message, string primaryText = "Да", string closeText = "Отмена");
    Task ShowMessageAsync(string title, string message, string closeText = "ОК");
    Task<VersionArchiveEntry?> ShowRollbackDialogAsync(TrackedApp app);
    Task ShowAppDetailsDialogAsync(TrackedAppItem item, IReadOnlyList<UpdateHistoryEntry> history, Action<bool> onConsentChanged);
}
