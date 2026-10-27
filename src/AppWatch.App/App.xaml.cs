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
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.Windows.AppNotifications;
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
            private readonly System.Threading.CancellationTokenSource _hostedCts = new();

    public App()
        {
            // Surface XAML-dispatched exceptions to crash.log before the stowed crash kills us
            UnhandledException += (_, e) =>
            {
                try
                {
                    var p = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "AppWatch", "logs", "crash.log");
                    Directory.CreateDirectory(Path.GetDirectoryName(p)!);
                    File.AppendAllText(p,
                        $"[{DateTime.Now}] XAML Unhandled: {e.Exception.GetType().Name}: {e.Message}\n" +
                        $"{e.Exception.StackTrace}\n");
                }
                catch { /* nothing more we can do */ }
            };

            InitializeComponent();
        }

    /// <summary>ZuiV2 pattern: pages resolve shared services through the App.</summary>
    public static T Resolve<T>() where T : notnull => (T)((App)Current).Services.GetRequiredService(typeof(T));

    protected override void OnLaunched(LaunchActivatedEventArgs args)
            {
                // Build DI container FIRST — it's fast and MainWindow constructor needs it
                // (WireNavBadges calls App.Resolve<MainViewModel>)
                var configStore = new JsonConfigStore();
                var config = configStore.Load();

                Services = new ServiceCollection()
                                                .AddSingleton(configStore)
                                                .AddSingleton(config)
                                                .AddSingleton<IConfigStore>(sp => sp.GetRequiredService<JsonConfigStore>())
                                                .AddSingleton<IGitHubClient>(_ => new GitHubApiClient(config.GitHubToken, config.DownloadMirrors))
                                                .AddSingleton<IProcessService, ProcessService>()
                                                .AddSingleton<IAppLauncher, AppLauncher>()
                                                .AddSingleton<IElevationService, ElevationService>()
                                                .AddSingleton<FileStore>()
                                                .AddSingleton<InstallerTrustService>()
                                                .AddSingleton<Core.Domain.UpdateGate>()
                                                .AddSingleton<UpdateJournalStore>()
                                                .AddSingleton<VersionStore>(sp => new VersionStore(config.VersionCacheCount))
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
                                                .AddHostedService<BackgroundMonitor>()
                                                .AddHostedService<CatalogMonitor>()
                                                .AddLogging(builder => builder.AddDebug())
                                                .AddSingleton(LocalizationService.Instance)
                                                .AddSingleton<MainViewModel>()
                                                .BuildServiceProvider();

                LocalizationService.Instance.SetLanguage(config.Language);

                // CRITICAL: AddHostedService only auto-starts inside a real IHost.
                // We build a bare ServiceProvider, so the background monitors
                // (BackgroundMonitor, CatalogMonitor) never ran — the "auto check"
                // was silently dead. Start them explicitly here.
                foreach (var hosted in Services.GetServices<Microsoft.Extensions.Hosting.IHostedService>())
                {
                    _ = Task.Run(() => hosted.StartAsync(_hostedCts.Token));
                }

                // NOW create and show the window IMMEDIATELY — UI is visible while background work continues
                _window = new MainWindow();
                Window = _window;
                _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(_window);
                _window.AppWindow.Closing += OnWindowClosing;

                // Register converters BEFORE the first page loads (StaticResource lookups happen then)
                Resources["BoolToVisibility"] = new Converters.BoolToVisibilityConverter();
                Resources["InverseBool"] = new Converters.InverseBoolConverter();
                Resources["StringToBool"] = new Converters.StringToBoolConverter();
                Resources["NotNullToVisibility"] = new Converters.NotNullToVisibilityConverter();
                Resources["CountToVisibility"] = new Converters.CountToVisibilityConverter();
                Resources["InverseCountToVisibility"] = new Converters.InverseCountToVisibilityConverter();
                Resources["StringToBrushConverter"] = new Converters.StringToBrushConverter();
                Resources["InverseBoolToVisibility"] = new Converters.InverseBoolToVisibilityConverter();
                Resources["StringNotEmptyToVisibility"] = new Converters.StringNotEmptyToVisibilityConverter();

                // App notifications: register BEFORE the window exists (official WinAppSDK
                // sample order). Best-effort — a failure here must never take the app down.
                // DISABLED: causes stowed 0xC000027B crashes on startup. Toasts work without it.
                // try
                // {
                //     AppNotificationManager.Default.NotificationInvoked += OnNotificationInvoked;
                //     AppNotificationManager.Default.Register();
                // }
                // catch (Exception ex)
                // {
                //     Serilog.Log.Warning(ex, "app notification registration failed; toasts disabled");
                // }

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

                // Heavy initialization on background thread — window is already visible
                _ = Task.Run(async () =>
                {
                    try
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

                        // Startup check + onboarding MUST run on the UI thread: they mutate
                        // observable VM state (StatusText, card items, footer), and x:Bind
                        // setters touch TextBlock/ContentDialog directly — from a worker thread
                        // that's RPC_E_WRONG_THREAD (0x8001010E), which silently killed the
                        // startup check (auto-updates appeared broken). Network stays async;
                        // only the VM/UI mutations are marshaled here.
                        _window.DispatcherQueue.TryEnqueue(async () =>
                        {
                            try
                            {
                                var vm = Services.GetRequiredService<MainViewModel>();
                                await vm.RunStartupTasksAsync();
                                await vm.ShowOnboardingIfNeededAsync();
                            }
                            catch (Exception ex)
                            {
                                Log.Error(ex, "Startup tasks failed");
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Background initialization failed");
                    }
                });
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
                                    _tray?.Destroy();
                                    // stop the background monitors cleanly
                                    _hostedCts.Cancel();
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

            /// <summary>
            /// Toast activation: restores+focuses the window. The single consolidated
            /// "updates available" toast uses action=open, which also lands the user on the
            /// Приложения tab. (Per-app "Update" buttons were removed in favour of one summary notification.)
            /// NotificationInvoked arrives on a non-UI thread — marshaled via the main
            /// window's DispatcherQueue.
            /// </summary>
            private void OnNotificationInvoked(AppNotificationManager sender,
                AppNotificationActivatedEventArgs args)
            {
                var window = _window;
                if (window is null) return;

                window.DispatcherQueue.TryEnqueue(() =>
                {
                    NativeMethods.ShowWindow(_hwnd, NativeMethods.SwRestore);
                    NativeMethods.SetForegroundWindow(_hwnd);

                    var action = args.Arguments.TryGetValue("action", out var a) ? a : "";
                                        if (action != "open") return;

                                        // Toast click → open the tracked-apps page ("Мои приложения")
                                        _window!.NavigateTo(typeof(Views.TrackedAppsPage));
                });
            }
        }
