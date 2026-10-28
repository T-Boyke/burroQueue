using burroQueue.Domain.Model;
using burroQueue.Infrastructure.Configuration;
using burroQueue.Infrastructure.Network;
using burroQueue.Infrastructure.Persistence;
using burroQueue.Infrastructure.Telephony;
using burroQueue.Infrastructure.Web;
using burroQueue.Presentation;
using burroQueue.Presentation.Services;
using burroQueue.Presentation.Views;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace burroQueue.Tests.Presentation;

public sealed class AppLifecycleTests
{
    [Fact]
    public async Task InitializeApplicationAsync_ShouldStartAllServicesAndInitializeUi()
    {
        ServiceCollection services = new();

        BurroGroup group = new(GroupId.New(), "App Test Group");
        IAppSettingsService settings = Substitute.For<IAppSettingsService>();
        settings.CurrentSettings.Returns(new AppSettingsData { EnableWebDropServer = true });
        IPlaylistStorageService storage = Substitute.For<IPlaylistStorageService>();
        ITelephonyManager telephony = Substitute.For<ITelephonyManager>();
        ILanGroupService lan = Substitute.For<ILanGroupService>();
        IEmbeddedWebDropServer webDrop = Substitute.For<IEmbeddedWebDropServer>();
        ISystrayNotificationService systray = Substitute.For<ISystrayNotificationService>();
        ISingleInstanceManager singleInstance = Substitute.For<ISingleInstanceManager>();
        singleInstance.TryAcquireSingleInstance().Returns(true);

        MainWindow dummyMain = (MainWindow)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(MainWindow));
        SettingsWindow dummySettings = (SettingsWindow)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(SettingsWindow));

        services.AddSingleton(singleInstance);
        services.AddSingleton(group);
        services.AddSingleton(settings);
        services.AddSingleton(storage);
        services.AddSingleton(telephony);
        services.AddSingleton(lan);
        services.AddSingleton(webDrop);
        services.AddSingleton(systray);
        services.AddSingleton(dummyMain);
        services.AddTransient(_ => dummySettings);

        using ServiceProvider sp = services.BuildServiceProvider();

        bool windowShown = false;
        bool settingsShown = false;
        bool shutdownTriggered = false;

        Action? capturedOpenSettings = null;
        Action? capturedExitApp = null;
        systray.When(s => s.Initialize(
                dummyMain,
                Arg.Any<Action>(),
                Arg.Any<Action>(),
                Arg.Any<Action?>(),
                Arg.Any<Action?>(),
                Arg.Any<Action?>(),
                Arg.Any<Action?>()))
            .Do(call =>
            {
                capturedOpenSettings = call.ArgAt<Action>(1);
                capturedExitApp = call.ArgAt<Action>(2);
            });


        await App.InitializeApplicationAsync(
            sp,
            showWindow: _ => windowShown = true,
            showSettings: (_, _) => settingsShown = true,
            shutdownAction: () => shutdownTriggered = true);

        await settings.Received(1).LoadAsync(Arg.Any<CancellationToken>());
        await storage.Received(1).LoadPlaylistAsync(group.Playlist, Arg.Any<CancellationToken>());
        await telephony.Received(1).StartAsync(Arg.Any<CancellationToken>());
        await lan.Received(1).StartAsync(Arg.Any<CancellationToken>());
        await webDrop.Received(1).StartAsync(Arg.Any<CancellationToken>());
        windowShown.Should().BeTrue();

        // Trigger captured systray callbacks
        capturedOpenSettings.Should().NotBeNull();
        capturedOpenSettings!();
        settingsShown.Should().BeTrue();

        capturedExitApp.Should().NotBeNull();
        capturedExitApp!();
        shutdownTriggered.Should().BeTrue();
    }

    [Fact]
    public async Task ShutdownApplicationAsync_ShouldSavePlaylistAndDisposeServices()
    {
        ServiceCollection services = new();
        BurroGroup group = new(GroupId.New(), "App Test Group");
        IPlaylistStorageService storage = Substitute.For<IPlaylistStorageService>();
        ISystrayNotificationService systray = Substitute.For<ISystrayNotificationService>();

        services.AddSingleton(group);
        services.AddSingleton(storage);

        ServiceProvider sp = services.BuildServiceProvider();

        await App.ShutdownApplicationAsync(sp, systray);

        await storage.Received(1).SavePlaylistAsync(group.Playlist, Arg.Any<CancellationToken>());
        systray.Received(1).Dispose();
    }

    [Fact]
    public async Task ShutdownApplicationAsync_WhenStorageThrows_ShouldNotThrow()
    {
        ServiceCollection services = new();
        BurroGroup group = new(GroupId.New(), "App Test Group");
        IPlaylistStorageService storage = Substitute.For<IPlaylistStorageService>();
        storage.When(s => s.SavePlaylistAsync(Arg.Any<Playlist>(), Arg.Any<CancellationToken>()))
            .Do(_ => throw new IOException("Disk error"));

        services.AddSingleton(group);
        services.AddSingleton(storage);

        ServiceProvider sp = services.BuildServiceProvider();

        Func<Task> act = async () => await App.ShutdownApplicationAsync(sp, null);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ShutdownApplicationAsync_WhenNullServiceProvider_ShouldNotThrow()
    {
        Func<Task> act = async () => await App.ShutdownApplicationAsync(null, null);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task InitializeApplicationAsync_WhenSingleInstanceFails_ShouldSignalExistingAndShutdown()
    {
        ServiceCollection services = new();
        ISingleInstanceManager singleInstance = Substitute.For<ISingleInstanceManager>();
        singleInstance.TryAcquireSingleInstance().Returns(false);
        services.AddSingleton(singleInstance);

        using ServiceProvider sp = services.BuildServiceProvider();

        bool windowShown = false;
        bool shutdownTriggered = false;

        await App.InitializeApplicationAsync(
            sp,
            showWindow: _ => windowShown = true,
            showSettings: (_, _) => { },
            shutdownAction: () => shutdownTriggered = true);

        singleInstance.Received(1).SignalExistingInstance();
        shutdownTriggered.Should().BeTrue();
        windowShown.Should().BeFalse();
    }
}
