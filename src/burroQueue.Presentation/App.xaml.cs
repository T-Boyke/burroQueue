using System.Windows;
using burroQueue.Domain.Model;
using burroQueue.Infrastructure.Audio;
using burroQueue.Infrastructure.Configuration;
using burroQueue.Infrastructure.Network;
using burroQueue.Infrastructure.Telephony;
using burroQueue.Infrastructure.YouTube;
using burroQueue.Presentation.ViewModels;
using burroQueue.Presentation.Views;
using Microsoft.Extensions.DependencyInjection;

namespace burroQueue.Presentation;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ServiceCollection services = new();
        ConfigureServices(services);

        _serviceProvider = services.BuildServiceProvider();

        IAppSettingsService settings = _serviceProvider.GetRequiredService<IAppSettingsService>();
        await settings.LoadAsync().ConfigureAwait(true);

        ITelephonyManager telephony = _serviceProvider.GetRequiredService<ITelephonyManager>();
        await telephony.StartAsync().ConfigureAwait(true);

        ILanGroupService lan = _serviceProvider.GetRequiredService<ILanGroupService>();
        await lan.StartAsync().ConfigureAwait(true);

        MainWindow mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Domain
        services.AddSingleton(new BurroGroup(GroupId.New(), "Büro Jukebox"));

        // Infrastructure
        services.AddSingleton<IAppSettingsService, AppSettingsService>();
        services.AddSingleton<IYouTubeUrlParser, YouTubeUrlParser>();
        services.AddSingleton<IAudioOutputController, AudioOutputController>();
        services.AddSingleton<ILanGroupService, LanGroupService>();

        // Modulare Telephony Providers
        services.AddSingleton<ITelephonyProvider, SwyxItTelephonyProvider>();
        services.AddSingleton<ITelephonyProvider, WindowsCoreAudioTelephonyProvider>();
        services.AddSingleton<ITelephonyProvider, NullTelephonyProvider>();
        services.AddSingleton<ITelephonyManager, TelephonyManager>();

        // Presentation ViewModels & Views
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddSingleton<MainWindow>();
        services.AddTransient<SettingsWindow>();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_serviceProvider is not null)
        {
            await _serviceProvider.DisposeAsync().ConfigureAwait(false);
        }

        base.OnExit(e);
    }
}
