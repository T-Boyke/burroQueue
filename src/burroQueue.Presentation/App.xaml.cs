using System.IO;
using System.Windows;
using burroQueue.Domain.Model;
using burroQueue.Domain.Repositories;
using burroQueue.Domain.Services;
using burroQueue.Infrastructure.Audio;
using burroQueue.Infrastructure.Configuration;
using burroQueue.Infrastructure.Cryptography;
using burroQueue.Infrastructure.Diagnostics;
using burroQueue.Infrastructure.Network;
using burroQueue.Infrastructure.Performance;
using burroQueue.Infrastructure.Persistence;
using burroQueue.Infrastructure.Playback;
using burroQueue.Infrastructure.Telephony;
using burroQueue.Infrastructure.Web;
using burroQueue.Infrastructure.Windows;
using burroQueue.Infrastructure.YouTube;
using burroQueue.Presentation.Services;
using burroQueue.Presentation.ViewModels;
using burroQueue.Presentation.Views;
using Microsoft.Extensions.DependencyInjection;

namespace burroQueue.Presentation;

[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
public partial class App : System.Windows.Application
{
    private ServiceProvider? _serviceProvider;
    private ISystrayNotificationService? _systrayService;

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    internal static void LogStartup(string message)
    {
        try
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "burroQueue");
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "startup.log"), $"[{DateTime.UtcNow:O}] [PID {Environment.ProcessId}] [Thread {Environment.CurrentManagedThreadId}] {message}{Environment.NewLine}");
        }
        catch
        {
        }
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += (_, args) =>
        {
            LogStartup($"DISPATCHER UNHANDLED EXCEPTION: {args.Exception}");
            args.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            LogStartup($"APPDOMAIN UNHANDLED EXCEPTION: {args.ExceptionObject}");
        };
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            LogStartup($"UNOBSERVED TASK EXCEPTION: {args.Exception}");
        };
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        LogStartup("OnStartup started (ShutdownMode=OnExplicitShutdown)");

        try
        {
            ServiceCollection services = new();
            ConfigureServices(services);

            _serviceProvider = services.BuildServiceProvider();
            _systrayService = _serviceProvider.GetRequiredService<ISystrayNotificationService>();
            LogStartup("Services configured and built");

            await InitializeApplicationAsync(
                _serviceProvider,
                showWindow: w =>
                {
                    LogStartup("showWindow callback invoked");
                    Current.MainWindow = w;
                    w.Show();
                    w.WindowState = WindowState.Normal;
                    w.Activate();
                    w.Focus();
                    IntPtr hwnd = new System.Windows.Interop.WindowInteropHelper(w).Handle;
                    LogStartup($"MainWindow shown. HWND=0x{hwnd:X}, Left={w.Left}, Top={w.Top}, Width={w.Width}, Height={w.Height}, Visibility={w.Visibility}, WindowState={w.WindowState}, IsActive={w.IsActive}");
                },
                showSettings: (sw, mw) =>
                {
                    sw.Owner = mw;
                    sw.ShowDialog();
                },
                shutdownAction: () => Shutdown()).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            LogStartup($"CRITICAL STARTUP ERROR: {ex}");
            System.Windows.MessageBox.Show(
                $"burroQueue konnte nicht gestartet werden:\n\n{ex.Message}\n\n{ex.InnerException?.Message}",
                "burroQueue – Startfehler",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    internal static async Task InitializeApplicationAsync(
        IServiceProvider serviceProvider,
        Action<MainWindow> showWindow,
        Action<SettingsWindow, MainWindow> showSettings,
        Action shutdownAction)
    {
        LogStartup("InitializeApplicationAsync started");
        ISingleInstanceManager? singleInstance = serviceProvider.GetService<ISingleInstanceManager>();
        if (singleInstance is not null && !singleInstance.TryAcquireSingleInstance())
        {
            LogStartup("Another instance is already running. Signaling existing instance and exiting.");
            singleInstance.SignalExistingInstance();
            shutdownAction();
            return;
        }

        IAppSettingsService settings = serviceProvider.GetRequiredService<IAppSettingsService>();
        await settings.LoadAsync().ConfigureAwait(true);
        Localization.LocalizationManager.Instance.SetLanguage(settings.CurrentSettings?.AppLanguage);
        LogStartup("Settings loaded");

        IPlaylistStorageService storage = serviceProvider.GetRequiredService<IPlaylistStorageService>();
        BurroGroup group = serviceProvider.GetRequiredService<BurroGroup>();
        await storage.LoadPlaylistAsync(group.Playlist).ConfigureAwait(true);
        LogStartup($"Playlist loaded ({group.Playlist.QueuedTracks.Count} queued tracks)");

        ITelephonyManager telephony = serviceProvider.GetRequiredService<ITelephonyManager>();
        await telephony.StartAsync().ConfigureAwait(true);
        LogStartup("Telephony manager started");

        ILanGroupService lan = serviceProvider.GetRequiredService<ILanGroupService>();
        await lan.StartAsync().ConfigureAwait(true);
        LogStartup("LAN group service started");

        IEmbeddedWebDropServer webDrop = serviceProvider.GetRequiredService<IEmbeddedWebDropServer>();
        if (settings.CurrentSettings?.EnableWebDropServer == true)
        {
            await webDrop.StartAsync().ConfigureAwait(true);
            LogStartup($"WebDrop server started on port {webDrop.Port}");
        }
        else
        {
            LogStartup("WebDrop server is disabled in settings (default: off)");
        }

        MainWindow mainWindow = serviceProvider.GetRequiredService<MainWindow>();
        MainWindowViewModel? viewModel = mainWindow.DataContext as MainWindowViewModel;
        if (viewModel is not null)
        {
            await viewModel.InitializeAsync().ConfigureAwait(true);
        }
        LogStartup("MainWindow resolved successfully");

        IProcessEfficiencyManager? efficiency = serviceProvider.GetService<IProcessEfficiencyManager>();
        efficiency?.EnableEfficiencyMode();

        ISystrayNotificationService systrayService = serviceProvider.GetRequiredService<ISystrayNotificationService>();
        systrayService.Initialize(
            mainWindow,
            onOpenSettings: () =>
            {
                SettingsWindow settingsWindow = serviceProvider.GetRequiredService<SettingsWindow>();
                showSettings(settingsWindow, mainWindow);
                efficiency?.TrimMemoryWorkingSet();
            },
            onExitApp: shutdownAction,
            onTogglePlayPause: () =>
            {
                if (viewModel is null)
                {
                    return;
                }

                if (viewModel.PlaybackState == Domain.Model.PlaybackState.Playing)

                {
                    _ = viewModel.PauseAsync();
                }
                else
                {
                    _ = viewModel.PlayAsync();
                }
            },
            onSkipNext: () =>
            {
                if (viewModel is not null)
                {
                    _ = viewModel.SkipAsync();
                }
            },
            onToggleMute: () => viewModel?.ToggleMute(),
            onOpenAbout: () =>
            {
                AboutWindow aboutWindow = serviceProvider.GetRequiredService<AboutWindow>();
                aboutWindow.Owner = mainWindow;
                aboutWindow.ShowDialog();
                efficiency?.TrimMemoryWorkingSet();
            });
        LogStartup("Systray initialized");



        LogStartup("Invoking showWindow callback...");
        showWindow(mainWindow);

        efficiency?.TrimMemoryWorkingSet();
        _ = Task.Run(async () =>
        {
            efficiency?.EnableCurrentThreadEfficiencyMode();
            await Task.Delay(2500).ConfigureAwait(false);
            efficiency?.TrimMemoryWorkingSet();
        });

        singleInstance?.StartListeningForSecondaryInstance(() =>
        {
            LogStartup("Secondary instance launch detected via IPC signal. Restoring MainWindow.");
            if (Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
            {
                dispatcher.Invoke(() => showWindow(mainWindow));
            }
            else
            {
                showWindow(mainWindow);
            }
        });
        LogStartup("InitializeApplicationAsync completed");
    }

    internal static async Task ShutdownApplicationAsync(IServiceProvider? serviceProvider, ISystrayNotificationService? systrayService)
    {
        if (serviceProvider is not null)
        {
            try
            {
                IPlaylistStorageService storage = serviceProvider.GetRequiredService<IPlaylistStorageService>();
                BurroGroup group = serviceProvider.GetRequiredService<BurroGroup>();
                IAudioPlayerBackend? backend = serviceProvider.GetService<IAudioPlayerBackend>();
                if (backend is not null)
                {
                    storage.UpdatePlaybackCursor(new QueuePlaybackCursor(
                        group.Playlist.CurrentTrack?.VideoId ?? string.Empty,
                        group.Playlist.CurrentTrackIndex,
                        backend.Position,
                        group.PlaybackState == PlaybackState.Playing));
                }

                await storage.SavePlaylistAsync(group.Playlist).ConfigureAwait(false);
            }
            catch
            {
                // Unhandled persistence errors ignored on exit
            }

            systrayService?.Dispose();
            if (serviceProvider is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    internal static void ConfigureServices(IServiceCollection services)
    {
        // Domain
        services.AddSingleton(new BurroGroup(GroupId.New(), "Büro Jukebox"));
        services.AddSingleton<QueueShuffleEngine>();

        // Infrastructure
        services.AddSingleton<IEntropySource, HardwareCsprngEntropySource>();
        services.AddSingleton<IFavoriteQueueRepository, TomlFavoriteQueueRepository>();
        services.AddSingleton<IAppSettingsService, AppSettingsService>();
        services.AddSingleton<IPlaylistStorageService, PlaylistStorageService>();
        services.AddSingleton<IAudioDeviceEnumerator, WindowsAudioDeviceEnumerator>();
        services.AddSingleton<IStreamUriResolver, YouTubeAudioStreamResolver>();
        services.AddSingleton<IYouTubeUrlParser, YouTubeUrlParser>();
        services.AddSingleton<IYouTubeMetadataFetcher, YouTubeMetadataFetcher>();
        services.AddSingleton<IYouTubeMusicValidator, YouTubeMusicValidator>();
        services.AddSingleton<IYouTubePlaylistReader, YouTubePlaylistReader>();
        services.AddSingleton<IYouTubePlaylistWriter, YouTubePlaylistWriter>();
        services.AddSingleton<IAudioOutputController, AudioOutputController>();
        services.AddSingleton<IMediaStreamPlayer, WpfMediaStreamPlayer>();
        services.AddSingleton<IAudioPlayerBackend, AudioPlayerBackend>();
        services.AddSingleton<ILanGroupService, LanGroupService>();
        services.AddSingleton<ILocalNetworkInfoService, LocalNetworkInfoService>();
        services.AddSingleton<IQrCodeSvgGenerator, QrCodeSvgGenerator>();
        services.AddSingleton<IEmbeddedWebDropServer, EmbeddedWebDropServer>();
        services.AddSingleton<IWindowsAutostartService, WindowsAutostartService>();
        services.AddSingleton<IProcessEfficiencyManager, ProcessEfficiencyManager>();
        services.AddSingleton<IGitHubRepoStatsClient, GitHubRepoStatsClient>();


        // Modulare Telephony Providers
        services.AddSingleton<ITelephonyProvider, SwyxItTelephonyProvider>();
        services.AddSingleton<ITelephonyProvider, StarfaceTelephonyProvider>();
        services.AddSingleton<ITelephonyProvider, WindowsCoreAudioTelephonyProvider>();
        services.AddSingleton<ITelephonyProvider, NullTelephonyProvider>();
        services.AddSingleton<ITelephonyManager, TelephonyManager>();

        // Presentation Services & Views
        services.AddSingleton<IAvatarBitmapCacheService>(_ => AvatarBitmapCacheService.Shared);
        services.AddSingleton<IClipboardMonitorService, Win32ClipboardMonitorService>();
        services.AddSingleton<ISingleInstanceManager, SingleInstanceManager>();
        services.AddSingleton<ISystrayNotificationService, SystrayNotificationService>();
        services.AddSingleton<IGlobalHotkeyService, GlobalHotkeyService>();
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddSingleton<MainWindow>();
        services.AddTransient<SettingsWindow>();
        services.AddTransient<AboutWindow>();
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    protected override async void OnExit(ExitEventArgs e)
    {
        LogStartup($"OnExit invoked with ExitCode={e.ApplicationExitCode}");
        await ShutdownApplicationAsync(_serviceProvider, _systrayService).ConfigureAwait(false);
        base.OnExit(e);
    }
}
