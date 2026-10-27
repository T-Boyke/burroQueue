using burroQueue.Domain.Model;
using burroQueue.Infrastructure.Audio;
using burroQueue.Infrastructure.Configuration;
using burroQueue.Infrastructure.Network;
using burroQueue.Infrastructure.Network.Messages;
using burroQueue.Infrastructure.Playback;
using burroQueue.Infrastructure.Telephony;
using burroQueue.Infrastructure.Web;
using burroQueue.Infrastructure.YouTube;
using burroQueue.Presentation.ViewModels;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace burroQueue.Tests.Presentation;

public sealed class MainWindowViewModelNetworkSyncTests
{
    private readonly BurroGroup _group;
    private readonly IYouTubeUrlParser _urlParser;
    private readonly IYouTubeMetadataFetcher _metadataFetcher;
    private readonly ITelephonyManager _telephonyManager;
    private readonly ILanGroupService _lanGroupService;
    private readonly IAudioOutputController _audioController;
    private readonly IAppSettingsService _settingsService;
    private readonly IEmbeddedWebDropServer _webDropServer;
    private readonly ILocalNetworkInfoService _networkInfoService;
    private readonly IAudioPlayerBackend _audioPlayerBackend;

    public MainWindowViewModelNetworkSyncTests()
    {
        _group = new BurroGroup(GroupId.New(), "Test Office");
        _urlParser = Substitute.For<IYouTubeUrlParser>();
        _metadataFetcher = Substitute.For<IYouTubeMetadataFetcher>();
        _telephonyManager = Substitute.For<ITelephonyManager>();
        _lanGroupService = Substitute.For<ILanGroupService>();
        _audioController = Substitute.For<IAudioOutputController>();
        _settingsService = Substitute.For<IAppSettingsService>();
        _webDropServer = Substitute.For<IEmbeddedWebDropServer>();
        _networkInfoService = Substitute.For<ILocalNetworkInfoService>();
        _audioPlayerBackend = Substitute.For<IAudioPlayerBackend>();

        _settingsService.CurrentSettings.Returns(new AppSettingsData
        {
            ClientDisplayName = "LocalUser",
            AutoOpenCollaborativePlaylistOnJoin = false,
            CollaborativePlaylistUrl = null
        });

        _networkInfoService.GetWebDropUrl(8080).Returns("http://192.168.1.10:8080");
    }

    private MainWindowViewModel CreateViewModel()
    {
        return new MainWindowViewModel(
            _group,
            _urlParser,
            _metadataFetcher,
            _telephonyManager,
            _lanGroupService,
            _audioController,
            _settingsService,
            _webDropServer,
            _networkInfoService,
            _audioPlayerBackend);
    }

    [Fact]
    public void QueueSyncReceived_EnqueueAction_ShouldAddTrackToQueue()
    {
        MainWindowViewModel vm = CreateViewModel();
        Track current = new("curr123", new Uri("https://www.youtube.com/watch?v=curr123"), "Playing Song", "Alice", DateTimeOffset.UtcNow);
        _group.Playlist.Enqueue(current);

        QueueSyncMessage syncMsg = new(
            _group.Id.Value,
            "Enqueue",
            "dQw4w9WgXcQ",
            "https://www.youtube.com/watch?v=dQw4w9WgXcQ",
            "Never Gonna Give You Up",
            "Bob",
            DateTimeOffset.UtcNow);

        _lanGroupService.QueueSyncReceived += Raise.Event<EventHandler<QueueSyncMessage>>(this, syncMsg);

        vm.QueuedItems.Should().HaveCount(1);
        vm.QueuedItems[0].Title.Should().Be("Never Gonna Give You Up");
        vm.QueuedCount.Should().Be(1);
    }

    [Fact]
    public void QueueSyncReceived_RemoveAction_ShouldRemoveTrackFromQueue()
    {
        MainWindowViewModel vm = CreateViewModel();
        Track current = new("curr123", new Uri("https://www.youtube.com/watch?v=curr123"), "Playing Song", "Alice", DateTimeOffset.UtcNow);
        Track queued = new("dQw4w9WgXcQ", new Uri("https://www.youtube.com/watch?v=dQw4w9WgXcQ"), "Rick Astley", "Alice", DateTimeOffset.UtcNow);
        _group.Playlist.Enqueue(current);
        _group.Playlist.Enqueue(queued);

        QueueSyncMessage syncMsg = new(
            _group.Id.Value,
            "Remove",
            "dQw4w9WgXcQ",
            "https://www.youtube.com/watch?v=dQw4w9WgXcQ",
            "Rick Astley",
            "Bob",
            DateTimeOffset.UtcNow);

        _lanGroupService.QueueSyncReceived += Raise.Event<EventHandler<QueueSyncMessage>>(this, syncMsg);

        vm.QueuedItems.Should().BeEmpty();
        vm.QueuedCount.Should().Be(0);
    }

    [Fact]
    public void EnqueueRawUrl_ShouldBroadcastQueueSyncMessage()
    {
        MainWindowViewModel vm = CreateViewModel();
        string testUrl = "https://www.youtube.com/watch?v=dQw4w9WgXcQ";
        Uri parsedUri = new(testUrl);

        _urlParser.TryExtractVideoId(testUrl, out Arg.Any<string>(), out Arg.Any<Uri>())
            .Returns(x =>
            {
                x[1] = "dQw4w9WgXcQ";
                x[2] = parsedUri;
                return true;
            });

        vm.EnqueueRawUrl(testUrl);

        _lanGroupService.Received(1).SendQueueSyncAsync(
            _group.Id,
            "Enqueue",
            Arg.Is<Track>(t => t.VideoId == "dQw4w9WgXcQ"));
    }

    [Fact]
    public void RemoveTrack_ShouldBroadcastQueueSyncMessage()
    {
        MainWindowViewModel vm = CreateViewModel();
        Track current = new("curr123", new Uri("https://www.youtube.com/watch?v=curr123"), "Playing Song", "Alice", DateTimeOffset.UtcNow);
        Track track = new("dQw4w9WgXcQ", new Uri("https://www.youtube.com/watch?v=dQw4w9WgXcQ"), "Rick Astley", "Alice", DateTimeOffset.UtcNow);
        _group.Playlist.Enqueue(current);
        _group.Playlist.Enqueue(track);

        vm.RemoveTrack(track);

        _lanGroupService.Received(1).SendQueueSyncAsync(
            _group.Id,
            "Remove",
            Arg.Is<Track>(t => t.VideoId == "dQw4w9WgXcQ"));
    }

    [Fact]
    public async Task SkipVoteReceived_WhenThresholdReached_ShouldSkipSong()
    {
        MainWindowViewModel vm = CreateViewModel();
        Track track1 = new("track-1", new Uri("https://www.youtube.com/watch?v=track-1"), "Song 1", "Alice", DateTimeOffset.UtcNow);
        Track track2 = new("track-2", new Uri("https://www.youtube.com/watch?v=track-2"), "Song 2", "Bob", DateTimeOffset.UtcNow);
        _group.Playlist.Enqueue(track1);
        _group.Playlist.Enqueue(track2);
        _group.Play();

        // 3 distinct voters cast skip votes
        Guid v1 = Guid.NewGuid();
        Guid v2 = Guid.NewGuid();
        Guid v3 = Guid.NewGuid();

        _lanGroupService.SkipVoteReceived += Raise.Event<EventHandler<SkipVoteMessage>>(this, new SkipVoteMessage(_group.Id.Value, "track-1", v1, DateTimeOffset.UtcNow));
        _lanGroupService.SkipVoteReceived += Raise.Event<EventHandler<SkipVoteMessage>>(this, new SkipVoteMessage(_group.Id.Value, "track-1", v2, DateTimeOffset.UtcNow));
        _lanGroupService.SkipVoteReceived += Raise.Event<EventHandler<SkipVoteMessage>>(this, new SkipVoteMessage(_group.Id.Value, "track-1", v3, DateTimeOffset.UtcNow));

        _group.Playlist.CurrentTrack.Should().NotBeNull();
        _group.Playlist.CurrentTrack!.VideoId.Should().Be("track-2");
    }
}
