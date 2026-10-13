using System.IO;
using AppWatch.Catalog;
using AppWatch.Localization;
using AppWatch.Platform;
using AppWatch.ViewModels;
using AppWatch.Views;
using AppWatch.Core.Abstractions;
using AppWatch.Core.Domain.Models;
using AppWatch.Infrastructure;
using AppWatch.Infrastructure.Config;
using AppWatch.Infrastructure.GitHub;
using AppWatch.Infrastructure.System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Serilog;
using Velopack;

namespace AppWatch;

public partial class App : Application
{
    public IServiceProvider Services { get; private set; } = null!;
    public static bool StartHidden { get; internal set; }

    /// <summary>The main window; set in OnLaunched (ThemeService and pages rely on it).</summary>
    public static Window? Window { get; private set; }

    /// <summary>Typed main window for page navigation (ZuiV2 pattern).</summary>
    public static Views.MainWindow? MainWindow => Window as Views.MainWindow;

    private MainWindow? _window;
        private TrayService? _tray;
        private AutostartService? _autostart;
        private IntPtr _hwnd;
        private bool _exiting;
        private BackgroundMonitor? _backgroundMonitor;

    public App()
    {
        InitializeComponent();
    }

    /// <summary>ZuiV2 pattern: pages resolve shared services through the App.</summary>
    public static T Resolve<T>() where T : notnull => (T)((App)Current).Services.GetRequiredService(typeof(T));

    protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            var appData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AppWatch");
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.File(
                    Path.Combine(appData, "logs", "appwatch-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 14)
                .CreateLogger();

            // Initialize Velopack for self-updates
            VelopackApp.Build().Run();

            var configStore = new JsonConfigStore();
            var config = configStore.Load();

        Services = new ServiceCollection()
                            .AddSingleton(configStore)
                            .AddSingleton<IConfigStore>(sp => sp.GetRequiredService<JsonConfigStore>())
                            .AddSingleton<IGitHubClient>(_ => new GitHubApiClient(config.GitHubToken))
                            .AddSingleton<IProcessService, ProcessService>()
                            .AddSingleton<IAppLauncher, AppLauncher>()
                            .AddSingleton<IElevationService, ElevationService>()
                            .AddSingleton<FileStore>()
                            .AddSingleton<IUpdateEngine, UpdateEngine>()
                            .AddSingleton<WingetService>()
                            .AddSingleton<SystemVersionResolver>()
                            .AddSingleton<UpdateHistoryStore>()
                            .AddSingleton<CatalogService>()
                            .AddSingleton<AutostartService>()
                            .AddSingleton<TrayService>()
                            .AddSingleton<ThemeService>()
                            .AddSingleton<SettingsViewModel>()
                            .AddSingleton<SelfUpdateService>()
                            .AddSingleton<IToastService, ToastService>()
                            .AddSingleton<BackgroundMonitor>()
                            .AddLogging(builder => builder.AddDebug())
                            .AddSingleton(LocalizationService.Instance)
                            .AddSingleton<MainViewModel>()
                            .BuildServiceProvider();

                LocalizationService.Instance.SetLanguage(config.Language);

                _window = new MainWindow();
                Window = _window;
                _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(_window);
                _window.AppWindow.Closing += OnWindowClosing;

                _autostart = Services.GetRequiredService<AutostartService>();
                _tray = Services.GetRequiredService<TrayService>();
                _tray.TrayIconClicked += () => _tray?.ShowWindowRestore(_hwnd);
                _tray.ShowRequested += () => _tray?.ShowWindowRestore(_hwnd);
                _tray.ExitRequested += ExitApplication;
                _tray.CheckUpdatesRequested += () => _ = RunTrayCheckAsync();
                _tray.AutostartToggleRequested += ToggleAutostart;
                _tray.Create(_hwnd, IconGenerator.CreateDefaultIcon(), "AppWatch",
                    () => _autostart?.IsEnabled == true);

                if (!StartHidden)
                            _window.Activate();

                        SingleInstance.StartListening(_ => ActivateWindow());

                        // Start background monitor
                        _backgroundMonitor = Services.GetRequiredService<BackgroundMonitor>();
                        _backgroundMonitor.Start();
                    }

            private void OnWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
            {
                // close = minimize to tray; a real exit goes through the tray menu
                if (_exiting) return;
                args.Cancel = true;
                _tray?.HideWindow(_hwnd);
            }

            private void ExitApplication()
            {
                _exiting = true;
                _backgroundMonitor?.Stop();
                _tray?.Destroy();
                // WinUI 3 has no OnExit override — cleanup happens here, before Exit()
                Log.CloseAndFlush();
                Exit();
            }

            private async Task RunTrayCheckAsync()
            {
                try
                {
                    await Services.GetRequiredService<MainViewModel>().RunBackgroundCheckAsync();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "tray check failed");
                }
            }

            private void ToggleAutostart()
            {
                if (_autostart is null) return;
                var enabled = !_autostart.IsEnabled;
                var ok = _autostart.SetEnabled(enabled);
                var loc = LocalizationService.Instance;
                _tray?.ShowBalloon("AppWatch", ok
                    ? loc[enabled ? "TrayAutostartOn" : "TrayAutostartOff"]
                    : loc["error"]);
            }

            private void ActivateWindow()
            {
                var dispatcherQueue = DispatcherQueue.GetForCurrentThread();
                dispatcherQueue.TryEnqueue(() =>
                {
                    if (_window is null) return;
                    var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(_window);
                    NativeMethods.ShowWindow(hwnd, NativeMethods.SwRestore);
                    NativeMethods.SetForegroundWindow(hwnd);
                });
            }
        }
