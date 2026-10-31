using AppWatch.Core.Abstractions;
using AppWatch.Core.Domain.Models;
using AppWatch.Infrastructure;

namespace AppWatch.Services;

public sealed class AppImportCoordinator
{
    private readonly IDialogService _dialogService;
    private readonly IConfigStore _configStore;
    private readonly IUpdateEngine _engine;
    private readonly IShortcutService _shortcuts;
    private readonly SystemVersionResolver _systemVersion;
    private readonly string _installRoot;

    public AppImportCoordinator(
        IDialogService dialogService,
        IConfigStore configStore,
        IUpdateEngine engine,
        IShortcutService shortcuts,
        SystemVersionResolver systemVersion,
        string installRoot)
    {
        _dialogService = dialogService;
        _configStore = configStore;
        _engine = engine;
        _shortcuts = shortcuts;
        _systemVersion = systemVersion;
        _installRoot = installRoot;
    }

    public async Task<TrackedApp?> AddCustomAppAsync(Func<TrackedApp, Task> onAppAdded)
    {
        var (confirmed, app, installImmediately, createStart, createDesktop) =
            await _dialogService.ShowAddEditAppDialogAsync();

        if (!confirmed || app is null) return null;

        await ProcessNewAppAsync(app, installImmediately, createStart, createDesktop, onAppAdded);
        return app;
    }

    public async Task<int> ScanSystemAsync(Func<TrackedApp, Task> onAppAdded)
    {
        var apps = await _dialogService.ShowSystemScannerDialogAsync();
        if (apps is null || apps.Count == 0) return 0;

        foreach (var app in apps)
        {
            await ProcessNewAppAsync(app, installImmediately: false, createStart: false, createDesktop: false, onAppAdded);
        }

        return apps.Count;
    }

    public async Task<int> BatchImportAsync(string? initialText, Func<TrackedApp, Task> onAppAdded)
    {
        var apps = await _dialogService.ShowBatchImportDialogAsync(initialText);
        if (apps is null || apps.Count == 0) return 0;

        foreach (var app in apps)
        {
            await ProcessNewAppAsync(app, installImmediately: false, createStart: false, createDesktop: false, onAppAdded);
        }

        return apps.Count;
    }

    public async Task HandleDroppedFileAsync(string filePath, Func<TrackedApp, Task> onAppAdded)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return;

        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        if (ext is ".txt" or ".json" or ".md")
        {
            try
            {
                var text = await File.ReadAllTextAsync(filePath);
                await BatchImportAsync(text, onAppAdded);
                return;
            }
            catch { }
        }

        var (confirmed, app, installImmediately, createStart, createDesktop) =
            await _dialogService.ShowAddEditAppDialogAsync(prefillUrlOrFile: filePath);

        if (confirmed && app is not null)
        {
            await ProcessNewAppAsync(app, installImmediately, createStart, createDesktop, onAppAdded);
        }
    }

    public async Task HandleDroppedInputAsync(string input, Func<TrackedApp, Task> onAppAdded)
    {
        if (string.IsNullOrWhiteSpace(input)) return;
        input = input.Trim();

        if (File.Exists(input))
        {
            await HandleDroppedFileAsync(input, onAppAdded);
            return;
        }

        if (input.Contains('\n') || input.Contains('\r'))
        {
            await BatchImportAsync(input, onAppAdded);
            return;
        }

        var (confirmed, app, installImmediately, createStart, createDesktop) =
            await _dialogService.ShowAddEditAppDialogAsync(prefillUrlOrFile: input);

        if (confirmed && app is not null)
        {
            await ProcessNewAppAsync(app, installImmediately, createStart, createDesktop, onAppAdded);
        }
    }

    private async Task ProcessNewAppAsync(
        TrackedApp app,
        bool installImmediately,
        bool createStart,
        bool createDesktop,
        Func<TrackedApp, Task> onAppAdded)
    {
        if (app.UpdateKind is UpdateKind.PortableZip or UpdateKind.PortableExe)
        {
            if (string.IsNullOrWhiteSpace(app.InstallDirectory))
                app.InstallDirectory = Path.Combine(_installRoot, app.RepoName);
        }
        else if (app.UpdateKind is UpdateKind.Installer)
        {
            if (string.IsNullOrWhiteSpace(app.InstallDirectory))
                _systemVersion.AutoDetectInstalledApp(app);
        }

        var config = _configStore.Load();
        config.TrackedApps.Add(app);
        _configStore.Save(config);

        if (createStart && !string.IsNullOrWhiteSpace(app.InstallDirectory) && !string.IsNullOrWhiteSpace(app.ExecutableName))
        {
            var p = Path.Combine(app.InstallDirectory, app.ExecutableName);
            _shortcuts.CreateStartMenuShortcut(app.Name, p, app.Description);
        }
        if (createDesktop && !string.IsNullOrWhiteSpace(app.InstallDirectory) && !string.IsNullOrWhiteSpace(app.ExecutableName))
        {
            var p = Path.Combine(app.InstallDirectory, app.ExecutableName);
            _shortcuts.CreateDesktopShortcut(app.Name, p, app.Description);
        }

        await onAppAdded(app);

        if (installImmediately)
        {
            _ = Task.Run(async () =>
            {
                try { await _engine.InstallAsync(app); }
                catch (Exception ex) { Serilog.Log.Error(ex, "Failed to install {Name}", app.Name); }
            });
        }
    }
}
