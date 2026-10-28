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
using burroQueue.Presentation;
using burroQueue.Presentation.Services;
using burroQueue.Presentation.ViewModels;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace burroQueue.Tests.Presentation;

public sealed class AppServiceRegistrationTests
{
    [Fact]
    public async Task ConfigureServices_ShouldRegisterAllCoreServicesWithoutExceptions()
    {
        ServiceCollection services = new();

        App.ConfigureServices(services);

        await using ServiceProvider provider = services.BuildServiceProvider();

        // Domain
        provider.GetRequiredService<BurroGroup>().Should().NotBeNull();

        // Infrastructure
        provider.GetRequiredService<IAppSettingsService>().Should().NotBeNull();
        provider.GetRequiredService<IPlaylistStorageService>().Should().NotBeNull();
        provider.GetRequiredService<IAudioDeviceEnumerator>().Should().NotBeNull();
        provider.GetRequiredService<IStreamUriResolver>().Should().NotBeNull();
        provider.GetRequiredService<IYouTubeUrlParser>().Should().NotBeNull();
        provider.GetRequiredService<IYouTubeMetadataFetcher>().Should().NotBeNull();
        provider.GetRequiredService<IYouTubePlaylistReader>().Should().NotBeNull();
        provider.GetRequiredService<IYouTubePlaylistWriter>().Should().NotBeNull();
        provider.GetRequiredService<IAudioOutputController>().Should().NotBeNull();
        provider.GetRequiredService<IMediaStreamPlayer>().Should().NotBeNull();
        provider.GetRequiredService<IAudioPlayerBackend>().Should().NotBeNull();
        provider.GetRequiredService<ILanGroupService>().Should().NotBeNull();
        provider.GetRequiredService<ILocalNetworkInfoService>().Should().NotBeNull();
        provider.GetRequiredService<IEmbeddedWebDropServer>().Should().NotBeNull();
        provider.GetRequiredService<IWindowsAutostartService>().Should().NotBeNull();
        provider.GetRequiredService<burroQueue.Infrastructure.Performance.IProcessEfficiencyManager>().Should().NotBeNull();
        provider.GetRequiredService<ITelephonyManager>().Should().NotBeNull();

        // Presentation Services & ViewModels
        provider.GetRequiredService<ISingleInstanceManager>().Should().NotBeNull();
        provider.GetRequiredService<ISystrayNotificationService>().Should().NotBeNull();
        provider.GetRequiredService<IGlobalHotkeyService>().Should().NotBeNull();
        provider.GetRequiredService<MainWindowViewModel>().Should().NotBeNull();
        provider.GetRequiredService<SettingsViewModel>().Should().NotBeNull();
    }
}
