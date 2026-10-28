using System.IO;
using System.Windows;
using burroQueue.Domain.Model;
using burroQueue.Infrastructure.Audio;
using burroQueue.Infrastructure.Configuration;
using burroQueue.Infrastructure.Network;
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
        IAppSettingsService settings = serviceProvider.GetRequiredService<IAppSettingsService>();
        await settings.LoadAsync().ConfigureAwait(true);
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
        await webDrop.StartAsync().ConfigureAwait(true);
        LogStartup($"WebDrop server started on port {webDrop.Port}");

        LogStartup("Resolving MainWindow...");
        MainWindow mainWindow = serviceProvider.GetRequiredService<MainWindow>();
        LogStartup("MainWindow resolved successfully");

        ISystrayNotificationService systrayService = serviceProvider.GetRequiredService<ISystrayNotificationService>();
        systrayService.Initialize(
            mainWindow,
            onOpenSettings: () =>
            {
                SettingsWindow settingsWindow = serviceProvider.GetRequiredService<SettingsWindow>();
                showSettings(settingsWindow, mainWindow);
            },
            onExitApp: shutdownAction);
        LogStartup("Systray initialized");

        LogStartup("Invoking showWindow callback...");
        showWindow(mainWindow);
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

        // Infrastructure
        services.AddSingleton<IAppSettingsService, AppSettingsService>();
        services.AddSingleton<IPlaylistStorageService, PlaylistStorageService>();
        services.AddSingleton<IAudioDeviceEnumerator, WindowsAudioDeviceEnumerator>();
        services.AddSingleton<IStreamUriResolver, YouTubeAudioStreamResolver>();
        services.AddSingleton<IYouTubeUrlParser, YouTubeUrlParser>();
        services.AddSingleton<IYouTubeMetadataFetcher, YouTubeMetadataFetcher>();
        services.AddSingleton<IAudioOutputController, AudioOutputController>();
        services.AddSingleton<IMediaStreamPlayer, WpfMediaStreamPlayer>();
        services.AddSingleton<IAudioPlayerBackend, AudioPlayerBackend>();
        services.AddSingleton<ILanGroupService, LanGroupService>();
        services.AddSingleton<ILocalNetworkInfoService, LocalNetworkInfoService>();
        services.AddSingleton<IEmbeddedWebDropServer, EmbeddedWebDropServer>();
        services.AddSingleton<IWindowsAutostartService, WindowsAutostartService>();

        // Modulare Telephony Providers
        services.AddSingleton<ITelephonyProvider, SwyxItTelephonyProvider>();
        services.AddSingleton<ITelephonyProvider, WindowsCoreAudioTelephonyProvider>();
        services.AddSingleton<ITelephonyProvider, NullTelephonyProvider>();
        services.AddSingleton<ITelephonyManager, TelephonyManager>();

        // Presentation Services & Views
        services.AddSingleton<ISystrayNotificationService, SystrayNotificationService>();
        services.AddSingleton<IGlobalHotkeyService, GlobalHotkeyService>();
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddSingleton<MainWindow>();
        services.AddTransient<SettingsWindow>();
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    protected override async void OnExit(ExitEventArgs e)
    {
        LogStartup($"OnExit invoked with ExitCode={e.ApplicationExitCode}");
        await ShutdownApplicationAsync(_serviceProvider, _systrayService).ConfigureAwait(false);
        base.OnExit(e);
    }
}
