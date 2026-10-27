using burroQueue.Domain.Model;
using burroQueue.Infrastructure.Audio;
using burroQueue.Infrastructure.Configuration;
using burroQueue.Infrastructure.Network;
using burroQueue.Infrastructure.Network.Messages;
using burroQueue.Infrastructure.Persistence;
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
    private readonly IPlaylistStorageService _playlistStorage;

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
        _playlistStorage = Substitute.For<IPlaylistStorageService>();

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
            _audioPlayerBackend,
            _playlistStorage);
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

    [Fact]
    public void GroupActionReceived_Rename_ShouldUpdateGroupName()
    {
        MainWindowViewModel vm = CreateViewModel();

        _lanGroupService.GroupActionReceived += Raise.Event<EventHandler<GroupActionMessage>>(
            this,
            new GroupActionMessage(_group.Id.Value, Guid.NewGuid(), "Rename", "Remote Renamed Group", DateTimeOffset.UtcNow));

        vm.GroupName.Should().Be("Remote Renamed Group");
        _group.Name.Should().Be("Remote Renamed Group");
    }

    [Theory]
    [InlineData("Play", PlaybackState.Playing)]
    [InlineData("Pause", PlaybackState.Paused)]
    [InlineData("Stop", PlaybackState.Stopped)]
    public void PlaybackCommandReceived_ShouldUpdatePlaybackState(string command, PlaybackState expectedState)
    {
        Track track = new("track-1", new Uri("https://www.youtube.com/watch?v=track-1"), "Song 1", "Alice", DateTimeOffset.UtcNow);
        _group.Playlist.Enqueue(track);

        MainWindowViewModel vm = CreateViewModel();

        _lanGroupService.PlaybackCommandReceived += Raise.Event<EventHandler<PlaybackCommandMessage>>(
            this,
            new PlaybackCommandMessage(_group.Id.Value, Guid.NewGuid(), command, null, DateTimeOffset.UtcNow));

        vm.PlaybackState.Should().Be(expectedState);
    }

    [Fact]
    public void CollaborativePlaylistReceived_ShouldUpdateUrl()
    {
        MainWindowViewModel vm = CreateViewModel();

        _lanGroupService.CollaborativePlaylistReceived += Raise.Event<EventHandler<SharedPlaylistSyncMessage>>(
            this,
            new SharedPlaylistSyncMessage(_group.Id.Value, "https://www.youtube.com/playlist?list=PLsharedPlaylist123", "Alice", DateTimeOffset.UtcNow));

        vm.CollaborativePlaylistUrl.Should().Be("https://www.youtube.com/playlist?list=PLsharedPlaylist123");
    }

    [Fact]
    public void SkipVoteReceived_WhenDifferentTrackOrGroup_ShouldIgnore()
    {
        Track track = new("current-song", new Uri("https://youtube.com/watch?v=current-song"), "Song", "Alice", DateTimeOffset.UtcNow);
        _group.Playlist.Enqueue(track);

        MainWindowViewModel vm = CreateViewModel();

        // Vote for a different track ID
        _lanGroupService.SkipVoteReceived += Raise.Event<EventHandler<SkipVoteMessage>>(
            this,
            new SkipVoteMessage(_group.Id.Value, "different-track", Guid.NewGuid(), DateTimeOffset.UtcNow));

        vm.SkipVoteStatus.Should().Be("0 / 3 Votes");

        // Vote for a different group ID
        _lanGroupService.SkipVoteReceived += Raise.Event<EventHandler<SkipVoteMessage>>(
            this,
            new SkipVoteMessage(Guid.NewGuid(), "current-song", Guid.NewGuid(), DateTimeOffset.UtcNow));

        vm.SkipVoteStatus.Should().Be("0 / 3 Votes");
    }

    [Fact]
    public async Task PlaybackCommandReceived_Skip_ShouldAdvanceQueue()
    {
        Track t1 = new("track-1", new Uri("https://youtube.com/watch?v=track-1"), "Song 1", "Alice", DateTimeOffset.UtcNow);
        Track t2 = new("track-2", new Uri("https://youtube.com/watch?v=track-2"), "Song 2", "Bob", DateTimeOffset.UtcNow);
        _group.Playlist.Enqueue(t1);
        _group.Playlist.Enqueue(t2);

        MainWindowViewModel vm = CreateViewModel();

        _lanGroupService.PlaybackCommandReceived += Raise.Event<EventHandler<PlaybackCommandMessage>>(
            this,
            new PlaybackCommandMessage(_group.Id.Value, Guid.NewGuid(), "Skip", null, DateTimeOffset.UtcNow));

        await Task.Delay(50);
        _group.Playlist.CurrentTrack.Should().NotBeNull();
        _group.Playlist.CurrentTrack!.VideoId.Should().Be("track-2");
    }

    [Fact]
    public void QueueSyncReceived_EnqueueAction_WhenDuplicateTrack_ShouldIgnore()
    {
        Track current = new("curr123", new Uri("https://youtube.com/watch?v=curr123"), "Playing Song", "Alice", DateTimeOffset.UtcNow);
        _group.Playlist.Enqueue(current);

        MainWindowViewModel vm = CreateViewModel();

        QueueSyncMessage duplicate = new(
            _group.Id.Value,
            "Enqueue",
            "curr123",
            "https://youtube.com/watch?v=curr123",
            "Playing Song",
            "Bob",
            DateTimeOffset.UtcNow);

        _lanGroupService.QueueSyncReceived += Raise.Event<EventHandler<QueueSyncMessage>>(this, duplicate);

        vm.QueuedCount.Should().Be(0);
    }

    [Fact]
    public void QueueSyncReceived_EnqueueAction_WhenInvalidUri_ShouldIgnore()
    {
        MainWindowViewModel vm = CreateViewModel();

        QueueSyncMessage invalid = new(
            _group.Id.Value,
            "Enqueue",
            "bad-uri",
            "://invalid url",
            "Bad Song",
            "Bob",
            DateTimeOffset.UtcNow);

        _lanGroupService.QueueSyncReceived += Raise.Event<EventHandler<QueueSyncMessage>>(this, invalid);

        vm.QueuedCount.Should().Be(0);
    }

    [Fact]
    public void QueueSyncReceived_RemoveAction_WhenTrackNotFound_ShouldIgnore()
    {
        Track current = new("curr123", new Uri("https://youtube.com/watch?v=curr123"), "Playing Song", "Alice", DateTimeOffset.UtcNow);
        _group.Playlist.Enqueue(current);

        MainWindowViewModel vm = CreateViewModel();

        QueueSyncMessage nonExisting = new(
            _group.Id.Value,
            "Remove",
            "does-not-exist",
            "https://youtube.com/watch?v=does-not-exist",
            "Unknown",
            "Bob",
            DateTimeOffset.UtcNow);

        _lanGroupService.QueueSyncReceived += Raise.Event<EventHandler<QueueSyncMessage>>(this, nonExisting);

        vm.QueuedCount.Should().Be(0);
    }

    [Fact]
    public void GroupActionReceived_WhenDifferentGroupIdOrNonRename_ShouldIgnore()
    {
        MainWindowViewModel vm = CreateViewModel();

        // Wrong group ID
        _lanGroupService.GroupActionReceived += Raise.Event<EventHandler<GroupActionMessage>>(
            this,
            new GroupActionMessage(Guid.NewGuid(), Guid.NewGuid(), "Rename", "Ignored Group", DateTimeOffset.UtcNow));

        vm.GroupName.Should().Be("Test Office");

        // Non-rename action
        _lanGroupService.GroupActionReceived += Raise.Event<EventHandler<GroupActionMessage>>(
            this,
            new GroupActionMessage(_group.Id.Value, Guid.NewGuid(), "Leave", "User Left", DateTimeOffset.UtcNow));

        vm.GroupName.Should().Be("Test Office");
    }

    [Fact]
    public void CollaborativePlaylistReceived_WhenDifferentGroupId_ShouldIgnore()
    {
        MainWindowViewModel vm = CreateViewModel();

        _lanGroupService.CollaborativePlaylistReceived += Raise.Event<EventHandler<SharedPlaylistSyncMessage>>(
            this,
            new SharedPlaylistSyncMessage(Guid.NewGuid(), "https://youtube.com/playlist?list=PLdifferent", "Bob", DateTimeOffset.UtcNow));

        vm.CollaborativePlaylistUrl.Should().BeNull();
    }
}


