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

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;
    private ISystrayNotificationService? _systrayService;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ServiceCollection services = new();
        ConfigureServices(services);

        _serviceProvider = services.BuildServiceProvider();

        IAppSettingsService settings = _serviceProvider.GetRequiredService<IAppSettingsService>();
        await settings.LoadAsync().ConfigureAwait(true);

        IPlaylistStorageService storage = _serviceProvider.GetRequiredService<IPlaylistStorageService>();
        BurroGroup group = _serviceProvider.GetRequiredService<BurroGroup>();
        await storage.LoadPlaylistAsync(group.Playlist).ConfigureAwait(true);

        ITelephonyManager telephony = _serviceProvider.GetRequiredService<ITelephonyManager>();
        await telephony.StartAsync().ConfigureAwait(true);

        ILanGroupService lan = _serviceProvider.GetRequiredService<ILanGroupService>();
        await lan.StartAsync().ConfigureAwait(true);

        IEmbeddedWebDropServer webDrop = _serviceProvider.GetRequiredService<IEmbeddedWebDropServer>();
        await webDrop.StartAsync().ConfigureAwait(true);

        MainWindow mainWindow = _serviceProvider.GetRequiredService<MainWindow>();

        _systrayService = _serviceProvider.GetRequiredService<ISystrayNotificationService>();
        _systrayService.Initialize(
            mainWindow,
            onOpenSettings: () =>
            {
                SettingsWindow settingsWindow = _serviceProvider.GetRequiredService<SettingsWindow>();
                settingsWindow.Owner = mainWindow;
                settingsWindow.ShowDialog();
            },
            onExitApp: () => Shutdown());

        mainWindow.Show();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Domain
        services.AddSingleton(new BurroGroup(GroupId.New(), "Büro Jukebox"));

        // Infrastructure
        services.AddSingleton<IAppSettingsService, AppSettingsService>();
        services.AddSingleton<IPlaylistStorageService, PlaylistStorageService>();
        services.AddSingleton<IYouTubeUrlParser, YouTubeUrlParser>();
        services.AddSingleton<IYouTubeMetadataFetcher, YouTubeMetadataFetcher>();
        services.AddSingleton<IAudioOutputController, AudioOutputController>();
        services.AddSingleton<IAudioPlayerBackend, AudioPlayerBackend>();
        services.AddSingleton<ILanGroupService, LanGroupService>();
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
        if (_serviceProvider is not null)
        {
            try
            {
                IPlaylistStorageService storage = _serviceProvider.GetRequiredService<IPlaylistStorageService>();
                BurroGroup group = _serviceProvider.GetRequiredService<BurroGroup>();
                await storage.SavePlaylistAsync(group.Playlist).ConfigureAwait(false);
            }
            catch
            {
                // Unhandled persistence errors ignored on exit
            }

            _systrayService?.Dispose();
            await _serviceProvider.DisposeAsync().ConfigureAwait(false);
        }

        base.OnExit(e);
    }
}
