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

public partial class App : System.Windows.Application
{
    private ServiceProvider? _serviceProvider;
    private ISystrayNotificationService? _systrayService;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ServiceCollection services = new();
        ConfigureServices(services);

        _serviceProvider = services.BuildServiceProvider();
        _systrayService = _serviceProvider.GetRequiredService<ISystrayNotificationService>();

        await InitializeApplicationAsync(
            _serviceProvider,
            showWindow: w => w.Show(),
            showSettings: (sw, mw) =>
            {
                sw.Owner = mw;
                sw.ShowDialog();
            },
            shutdownAction: () => Shutdown()).ConfigureAwait(true);
    }

    internal static async Task InitializeApplicationAsync(
        IServiceProvider serviceProvider,
        Action<MainWindow> showWindow,
        Action<SettingsWindow, MainWindow> showSettings,
        Action shutdownAction)
    {
        IAppSettingsService settings = serviceProvider.GetRequiredService<IAppSettingsService>();
        await settings.LoadAsync().ConfigureAwait(true);

        IPlaylistStorageService storage = serviceProvider.GetRequiredService<IPlaylistStorageService>();
        BurroGroup group = serviceProvider.GetRequiredService<BurroGroup>();
        await storage.LoadPlaylistAsync(group.Playlist).ConfigureAwait(true);

        ITelephonyManager telephony = serviceProvider.GetRequiredService<ITelephonyManager>();
        await telephony.StartAsync().ConfigureAwait(true);

        ILanGroupService lan = serviceProvider.GetRequiredService<ILanGroupService>();
        await lan.StartAsync().ConfigureAwait(true);

        IEmbeddedWebDropServer webDrop = serviceProvider.GetRequiredService<IEmbeddedWebDropServer>();
        await webDrop.StartAsync().ConfigureAwait(true);

        MainWindow mainWindow = serviceProvider.GetRequiredService<MainWindow>();

        ISystrayNotificationService systrayService = serviceProvider.GetRequiredService<ISystrayNotificationService>();
        systrayService.Initialize(
            mainWindow,
            onOpenSettings: () =>
            {
                SettingsWindow settingsWindow = serviceProvider.GetRequiredService<SettingsWindow>();
                showSettings(settingsWindow, mainWindow);
            },
            onExitApp: shutdownAction);

        showWindow(mainWindow);
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

    protected override async void OnExit(ExitEventArgs e)
    {
        await ShutdownApplicationAsync(_serviceProvider, _systrayService).ConfigureAwait(false);
        base.OnExit(e);
    }
}
