using System.Net;
using System.Net.Sockets;
using burroQueue.Domain.Model;
using burroQueue.Infrastructure.Audio;
using burroQueue.Infrastructure.Configuration;
using burroQueue.Infrastructure.Network;
using burroQueue.Infrastructure.Persistence;
using burroQueue.Infrastructure.Playback;
using burroQueue.Infrastructure.Telephony;
using burroQueue.Infrastructure.Web;
using burroQueue.Infrastructure.YouTube;
using burroQueue.Presentation.ViewModels;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace burroQueue.Tests.E2E;

public sealed class EndToEndWorkflowTests : IAsyncLifetime, IDisposable
{
    private readonly string _tempSettingsFile;
    private readonly string _tempPlaylistFile;
    private readonly int _webDropPort;
    private ServiceProvider? _provider;

    public EndToEndWorkflowTests()
    {
        _tempSettingsFile = Path.Combine(Path.GetTempPath(), $"burro_e2e_settings_{Guid.NewGuid():N}.json");
        _tempPlaylistFile = Path.Combine(Path.GetTempPath(), $"burro_e2e_playlist_{Guid.NewGuid():N}.json");

        using TcpListener tcp = new(IPAddress.Loopback, 0);
        tcp.Start();
        _webDropPort = ((IPEndPoint)tcp.LocalEndpoint).Port;
        tcp.Stop();
    }

    public async Task InitializeAsync()
    {
        ServiceCollection services = new();

        // Domain
        BurroGroup group = new(GroupId.New(), "E2E Test Jukebox");
        services.AddSingleton(group);

        // Infrastructure with temp paths
        services.AddSingleton<IAppSettingsService>(new AppSettingsService(_tempSettingsFile));
        services.AddSingleton<IPlaylistStorageService>(new PlaylistStorageService(_tempPlaylistFile));
        services.AddSingleton<IYouTubeUrlParser, YouTubeUrlParser>();

        // Mock external YouTube HTTP metadata fetcher to return realistic track title
        IYouTubeMetadataFetcher metadataFetcher = Substitute.For<IYouTubeMetadataFetcher>();
        metadataFetcher.FetchMetadataAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => new TrackMetadata("E2E Anthem", "E2E Artist", null));
        services.AddSingleton(metadataFetcher);

        // Mock playback backend (no physical sound card required in test)
        IAudioPlayerBackend audioBackend = Substitute.For<IAudioPlayerBackend>();
        services.AddSingleton(audioBackend);
        services.AddSingleton<IAudioOutputController, AudioOutputController>();

        // Network services
        services.AddSingleton<ILocalNetworkInfoService, LocalNetworkInfoService>();
        services.AddSingleton<ILanGroupService, LanGroupService>();
        services.AddSingleton<IEmbeddedWebDropServer>(new EmbeddedWebDropServer(_webDropPort, group));

        // Telephony
        services.AddSingleton<ITelephonyProvider, NullTelephonyProvider>();
        services.AddSingleton<ITelephonyManager, TelephonyManager>();

        // Presentation
        services.AddTransient<MainWindowViewModel>();

        _provider = services.BuildServiceProvider();

        // Start services
        await _provider.GetRequiredService<IAppSettingsService>().LoadAsync();
        await _provider.GetRequiredService<IEmbeddedWebDropServer>().StartAsync();
        await _provider.GetRequiredService<ILanGroupService>().StartAsync();
    }

    public async Task DisposeAsync()
    {
        if (_provider is not null)
        {
            await _provider.DisposeAsync();
            _provider = null;
        }

        if (File.Exists(_tempSettingsFile))
        {
            File.Delete(_tempSettingsFile);
        }

        if (File.Exists(_tempPlaylistFile))
        {
            File.Delete(_tempPlaylistFile);
        }
    }

    public void Dispose()
    {
        DisposeAsync().GetAwaiter().GetResult();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task CompleteE2EWorkflow_DropTrack_Play_Skip_PersistAndRestore()
    {
        MainWindowViewModel vm = _provider!.GetRequiredService<MainWindowViewModel>();
        BurroGroup group = _provider!.GetRequiredService<BurroGroup>();
        IPlaylistStorageService storage = _provider!.GetRequiredService<IPlaylistStorageService>();
        IAudioPlayerBackend audio = _provider!.GetRequiredService<IAudioPlayerBackend>();

        // 1. Initial State: Empty
        vm.QueuedCount.Should().Be(0);
        vm.PlaybackState.Should().Be(PlaybackState.Stopped);

        // 2. Drop 2 YouTube tracks via WebDrop HTTP POST
        using HttpClient client = new();
        Dictionary<string, string> form1 = new() { ["url"] = "https://www.youtube.com/watch?v=dQw4w9WgXcQ" };
        HttpResponseMessage post1 = await client.PostAsync($"http://localhost:{_webDropPort}/drop", new FormUrlEncodedContent(form1));
        post1.StatusCode.Should().Be(HttpStatusCode.OK);

        Dictionary<string, string> form2 = new() { ["url"] = "https://youtu.be/e-ORhEE9VVg" };
        HttpResponseMessage post2 = await client.PostAsync($"http://localhost:{_webDropPort}/drop", new FormUrlEncodedContent(form2));
        post2.StatusCode.Should().Be(HttpStatusCode.OK);

        // Brief delay for event loop
        await Task.Delay(150);

        // 3. Verify Enqueued in ViewModel and Group
        group.Playlist.CurrentTrack.Should().NotBeNull();
        group.Playlist.CurrentTrack!.VideoId.Should().Be("dQw4w9WgXcQ");
        group.Playlist.QueuedTracks.Should().ContainSingle().Which.VideoId.Should().Be("e-ORhEE9VVg");
        vm.QueuedCount.Should().Be(1);

        // 4. Start Playback
        await vm.PlayAsync();
        vm.PlaybackState.Should().Be(PlaybackState.Playing);
        await audio.Received(1).PlayTrackAsync(group.Playlist.CurrentTrack!, Arg.Any<CancellationToken>());

        // 5. Skip Track
        await vm.SkipAsync();
        group.Playlist.CurrentTrack!.VideoId.Should().Be("e-ORhEE9VVg");
        await audio.Received(1).PlayTrackAsync(Arg.Is<Track>(t => t.VideoId == "e-ORhEE9VVg"), Arg.Any<CancellationToken>());

        // 6. Persist Playlist
        await storage.SavePlaylistAsync(group.Playlist);
        File.Exists(_tempPlaylistFile).Should().BeTrue();

        // 7. Restore Playlist in fresh group
        Playlist restored = new();
        await storage.LoadPlaylistAsync(restored);
        restored.CurrentTrack.Should().NotBeNull();
        restored.CurrentTrack!.VideoId.Should().Be("e-ORhEE9VVg");
    }
}
