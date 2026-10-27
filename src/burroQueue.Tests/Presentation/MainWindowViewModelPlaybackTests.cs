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
using NSubstitute;
using Xunit;

namespace burroQueue.Tests.Presentation;

public sealed class MainWindowViewModelPlaybackTests
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

    public MainWindowViewModelPlaybackTests()
    {
        _group = new BurroGroup(GroupId.New(), "Playback Office");
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
            ClientDisplayName = "PlaybackUser",
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
    public async Task PlayAsync_WhenCurrentTrackExists_ShouldInvokeAudioPlayerPlayTrackAsync()
    {
        Track track = new("track-1", new Uri("https://www.youtube.com/watch?v=track-1"), "Song 1", "Alice", DateTimeOffset.UtcNow);
        _group.Playlist.Enqueue(track);

        MainWindowViewModel vm = CreateViewModel();

        await vm.PlayAsync();

        await _audioPlayerBackend.Received(1).PlayTrackAsync(track, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PauseAsync_ShouldInvokeAudioPlayerPauseAsync()
    {
        MainWindowViewModel vm = CreateViewModel();

        await vm.PauseAsync();

        await _audioPlayerBackend.Received(1).PauseAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StopAsync_ShouldInvokeAudioPlayerStopAsync()
    {
        MainWindowViewModel vm = CreateViewModel();

        await vm.StopAsync();

        await _audioPlayerBackend.Received(1).StopAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SkipAsync_WhenNextTrackExists_ShouldPlayNextTrack()
    {
        Track track1 = new("track-1", new Uri("https://www.youtube.com/watch?v=track-1"), "Song 1", "Alice", DateTimeOffset.UtcNow);
        Track track2 = new("track-2", new Uri("https://www.youtube.com/watch?v=track-2"), "Song 2", "Bob", DateTimeOffset.UtcNow);
        _group.Playlist.Enqueue(track1);
        _group.Playlist.Enqueue(track2);

        MainWindowViewModel vm = CreateViewModel();

        await vm.SkipAsync();

        await _audioPlayerBackend.Received(1).PlayTrackAsync(track2, Arg.Any<CancellationToken>());
        _group.Playlist.CurrentTrack!.VideoId.Should().Be("track-2");
    }

    [Fact]
    public async Task SkipAsync_WhenQueueEmpty_ShouldInvokeStopAsync()
    {
        Track track1 = new("track-1", new Uri("https://www.youtube.com/watch?v=track-1"), "Song 1", "Alice", DateTimeOffset.UtcNow);
        _group.Playlist.Enqueue(track1);

        MainWindowViewModel vm = CreateViewModel();

        await vm.SkipAsync();

        await _audioPlayerBackend.Received(1).StopAsync(Arg.Any<CancellationToken>());
        _group.Playlist.CurrentTrack.Should().BeNull();
    }

    [Fact]
    public async Task TrackPlaybackFinished_ShouldTriggerAutoAdvanceToNextTrack()
    {
        Track track1 = new("track-1", new Uri("https://www.youtube.com/watch?v=track-1"), "Song 1", "Alice", DateTimeOffset.UtcNow);
        Track track2 = new("track-2", new Uri("https://www.youtube.com/watch?v=track-2"), "Song 2", "Bob", DateTimeOffset.UtcNow);
        _group.Playlist.Enqueue(track1);
        _group.Playlist.Enqueue(track2);

        MainWindowViewModel vm = CreateViewModel();

        TaskCompletionSource<bool> tcs = new();
        _audioPlayerBackend.When(x => x.PlayTrackAsync(track2, Arg.Any<CancellationToken>()))
            .Do(_ => tcs.TrySetResult(true));

        _audioPlayerBackend.TrackPlaybackFinished += Raise.Event<EventHandler<Track>>(this, track1);

        await Task.WhenAny(tcs.Task, Task.Delay(2000));
        tcs.Task.IsCompletedSuccessfully.Should().BeTrue();
        _group.Playlist.CurrentTrack!.VideoId.Should().Be("track-2");
    }

    [Fact]
    public void ToggleQueueExpanded_ShouldInvertIsQueueExpanded()
    {
        MainWindowViewModel vm = CreateViewModel();
        vm.IsQueueExpanded.Should().BeFalse();

        vm.ToggleQueueExpanded();
        vm.IsQueueExpanded.Should().BeTrue();

        vm.ToggleQueueExpanded();
        vm.IsQueueExpanded.Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("invalid-youtube-url")]
    public void EnqueueRawUrl_WithInvalidOrEmptyUrl_ShouldSetStatusMessage(string? url)
    {
        _urlParser.TryExtractVideoId(Arg.Any<string>(), out Arg.Any<string>(), out Arg.Any<Uri>())
            .Returns(false);

        MainWindowViewModel vm = CreateViewModel();
        vm.EnqueueRawUrl(url);

        vm.StatusMessage.Should().Be("Invalid YouTube URL");
        vm.QueuedCount.Should().Be(0);
    }

    [Fact]
    public void RemoveTrack_WithNull_ShouldDoNothing()
    {
        MainWindowViewModel vm = CreateViewModel();

        Action act = () => vm.RemoveTrack(null);

        act.Should().NotThrow();
    }

    [Fact]
    public async Task VoteToSkipAsync_WhenNoCurrentTrack_ShouldDoNothing()
    {
        MainWindowViewModel vm = CreateViewModel();

        await vm.VoteToSkipAsync();

        await _lanGroupService.DidNotReceive().SendSkipVoteAsync(Arg.Any<GroupId>(), Arg.Any<string>(), Arg.Any<ClientId>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task VoteToSkipAsync_WhenThresholdReached_ShouldTriggerSkip()
    {
        Track track1 = new("track-1", new Uri("https://www.youtube.com/watch?v=track-1"), "Song 1", "Alice", DateTimeOffset.UtcNow);
        Track track2 = new("track-2", new Uri("https://www.youtube.com/watch?v=track-2"), "Song 2", "Bob", DateTimeOffset.UtcNow);
        _group.Playlist.Enqueue(track1);
        _group.Playlist.Enqueue(track2);

        MainWindowViewModel vm = CreateViewModel();

        // 1st vote from local user (threshold is 3 votes)
        await vm.VoteToSkipAsync();
        vm.SkipVoteStatus.Should().Be("1 / 3 Votes");

        // Simulate 2nd vote from peer
        _lanGroupService.SkipVoteReceived += Raise.Event<EventHandler<burroQueue.Infrastructure.Network.Messages.SkipVoteMessage>>(
            this,
            new burroQueue.Infrastructure.Network.Messages.SkipVoteMessage(_group.Id.Value, "track-1", Guid.NewGuid(), DateTimeOffset.UtcNow));

        // Simulate 3rd vote from another peer -> reaches 3 votes -> triggers skip!
        _lanGroupService.SkipVoteReceived += Raise.Event<EventHandler<burroQueue.Infrastructure.Network.Messages.SkipVoteMessage>>(
            this,
            new burroQueue.Infrastructure.Network.Messages.SkipVoteMessage(_group.Id.Value, "track-1", Guid.NewGuid(), DateTimeOffset.UtcNow));

        _group.Playlist.CurrentTrack.Should().NotBeNull();
        _group.Playlist.CurrentTrack!.VideoId.Should().Be("track-2");
    }

    [Fact]
    public async Task RenameGroupAsync_WithValidName_ShouldUpdateGroupNameAndBroadcast()
    {
        MainWindowViewModel vm = CreateViewModel();

        await vm.RenameGroupAsync("Studio Jukebox");

        vm.GroupName.Should().Be("Studio Jukebox");
        _group.Name.Should().Be("Studio Jukebox");
        await _lanGroupService.Received(1).SendGroupActionAsync(
            _group.Id,
            Arg.Any<ClientId>(),
            "Rename",
            "Studio Jukebox",
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RenameGroupAsync_WithEmptyOrWhitespace_ShouldNotRename(string? emptyName)
    {
        MainWindowViewModel vm = CreateViewModel();

        await vm.RenameGroupAsync(emptyName);

        vm.GroupName.Should().Be("Playback Office");
        await _lanGroupService.DidNotReceive().SendGroupActionAsync(
            Arg.Any<GroupId>(),
            Arg.Any<ClientId>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void TelephonyStateChanged_WhenCallStarts_ShouldDuckVolumeAndSetStatus()
    {
        MainWindowViewModel vm = CreateViewModel();

        _telephonyManager.StateChanged += Raise.Event<EventHandler<TelephonyCallEventArgs>>(
            this,
            new TelephonyCallEventArgs("WindowsCoreAudio", TelephonyState.ActiveCall));

        vm.IsCallActive.Should().BeTrue();
        vm.TelephonyStatusText.Should().Contain("WindowsCoreAudio Anruf aktiv");
        _audioController.Received(1).SetTargetVolume(0.12, true);
    }

    [Fact]
    public void TelephonyStateChanged_WhenCallEnds_ShouldRestoreVolumeAndClearStatus()
    {
        MainWindowViewModel vm = CreateViewModel();

        _telephonyManager.StateChanged += Raise.Event<EventHandler<TelephonyCallEventArgs>>(
            this,
            new TelephonyCallEventArgs("WindowsCoreAudio", TelephonyState.Idle));

        vm.IsCallActive.Should().BeFalse();
        vm.TelephonyStatusText.Should().BeEmpty();
        _audioController.Received(1).SetTargetVolume(0.8, false);
    }

    [Fact]
    public async Task EnqueueRawUrl_WhenValid_ShouldTriggerPlaylistAutoSave()
    {
        _urlParser.TryExtractVideoId("https://youtube.com/watch?v=save-123", out Arg.Any<string>()!, out Arg.Any<Uri>()!)
            .Returns(x =>
            {
                x[1] = "save-123";
                x[2] = new Uri("https://youtube.com/watch?v=save-123");
                return true;
            });

        MainWindowViewModel vm = CreateViewModel();

        vm.EnqueueRawUrl("https://youtube.com/watch?v=save-123");

        await vm.AutoSaveTask;
        await _playlistStorage.Received().SavePlaylistAsync(_group.Playlist, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RemoveTrack_WhenValid_ShouldTriggerPlaylistAutoSave()
    {
        Track track = new("rem-123", new Uri("https://youtube.com/watch?v=rem-123"), "Remove Song", "Alice", DateTimeOffset.UtcNow);
        _group.Playlist.Enqueue(track);

        Track track2 = new("rem-456", new Uri("https://youtube.com/watch?v=rem-456"), "Keep Song", "Alice", DateTimeOffset.UtcNow);
        _group.Playlist.Enqueue(track2);

        MainWindowViewModel vm = CreateViewModel();

        vm.RemoveTrack(track2);

        await vm.AutoSaveTask;
        await _playlistStorage.Received().SavePlaylistAsync(_group.Playlist, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SkipAsync_ShouldTriggerPlaylistAutoSave()
    {
        Track track1 = new("s1", new Uri("https://youtube.com/watch?v=s1"), "Song 1", "Alice", DateTimeOffset.UtcNow);
        Track track2 = new("s2", new Uri("https://youtube.com/watch?v=s2"), "Song 2", "Bob", DateTimeOffset.UtcNow);
        _group.Playlist.Enqueue(track1);
        _group.Playlist.Enqueue(track2);

        MainWindowViewModel vm = CreateViewModel();

        await vm.SkipAsync();

        await vm.AutoSaveTask;
        await _playlistStorage.Received().SavePlaylistAsync(_group.Playlist, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InitializeAsync_ShouldLoadPlaylistAndSyncUi()
    {
        _playlistStorage.LoadPlaylistAsync(_group.Playlist, Arg.Any<CancellationToken>())
            .Returns(x =>
            {
                _group.Playlist.Enqueue(new Track("init-1", new Uri("https://youtube.com/watch?v=init-1"), "Loaded", "Storage", DateTimeOffset.UtcNow));
                return Task.CompletedTask;
            });

        MainWindowViewModel vm = CreateViewModel();

        await vm.InitializeAsync();

        await _playlistStorage.Received(1).LoadPlaylistAsync(_group.Playlist, Arg.Any<CancellationToken>());
        vm.QueuedCount.Should().Be(0); // 1 track is CurrentTrack, so QueuedCount (waiting) is 0
        _group.Playlist.CurrentTrack.Should().NotBeNull();
        _group.Playlist.CurrentTrack!.VideoId.Should().Be("init-1");
    }

    [Fact]
    public void CopyWebDropUrl_ShouldSetStatusMessage()
    {
        MainWindowViewModel vm = CreateViewModel();

        Action act = () => vm.CopyWebDropUrl();

        act.Should().NotThrow();
        vm.StatusMessage.Should().Match(s => s == "WebDrop-Link kopiert!" || s == "Kopieren fehlgeschlagen");
    }

    [Fact]
    public void OpenCollaborativePlaylist_ShouldNotThrow()
    {
        MainWindowViewModel vm = CreateViewModel();

        Action act = () => vm.OpenCollaborativePlaylist();

        act.Should().NotThrow();
    }

    [Fact]
    public async Task InitializeAsync_WhenStorageThrows_ShouldCatchGracefully()
    {
        _playlistStorage.When(s => s.LoadPlaylistAsync(Arg.Any<Playlist>(), Arg.Any<CancellationToken>()))
            .Do(_ => throw new IOException("Disk error"));

        MainWindowViewModel vm = CreateViewModel();

        Func<Task> act = async () => await vm.InitializeAsync();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task VoteToSkipAsync_WhenThresholdNotReached_ShouldUpdateStatusWithoutSkipping()
    {
        Track track = new("t1", new Uri("https://youtube.com/watch?v=t1"), "Song", "Alice", DateTimeOffset.UtcNow);
        _group.Playlist.Enqueue(track);

        MainWindowViewModel vm = CreateViewModel();

        // Single vote does not reach required threshold of 3
        await vm.VoteToSkipAsync();

        vm.SkipVoteStatus.Should().Be("1 / 3 Votes");
        vm.StatusMessage.Should().NotContain("Skipping song");
        _group.Playlist.CurrentTrack.Should().Be(track);
    }

    [Fact]
    public void RemoveTrack_WhenTrackNotInQueue_ShouldNotThrowOrBroadcast()
    {
        Track inQueue = new("q1", new Uri("https://youtube.com/watch?v=q1"), "Queue 1", "Alice", DateTimeOffset.UtcNow);
        Track notInQueue = new("q2", new Uri("https://youtube.com/watch?v=q2"), "Queue 2", "Bob", DateTimeOffset.UtcNow);
        _group.Playlist.Enqueue(inQueue);

        MainWindowViewModel vm = CreateViewModel();

        vm.RemoveTrack(notInQueue);

        _lanGroupService.DidNotReceive().SendQueueSyncAsync(_group.Id, "Remove", notInQueue);
    }

    [Fact]
    public async Task EnqueueRawUrl_WhenFirstTrack_ShouldTriggerMetadataFetch()
    {
        _urlParser.TryExtractVideoId("https://youtube.com/watch?v=meta-1", out Arg.Any<string>()!, out Arg.Any<Uri>()!)
            .Returns(x =>
            {
                x[1] = "meta-1";
                x[2] = new Uri("https://youtube.com/watch?v=meta-1");
                return true;
            });

        _metadataFetcher.FetchMetadataAsync("meta-1", Arg.Any<CancellationToken>())
            .Returns(new TrackMetadata("Fetched Title", "Fetched Artist", new Uri("https://img.youtube.com/vi/meta-1/0.jpg")));

        MainWindowViewModel vm = CreateViewModel();

        vm.EnqueueRawUrl("https://youtube.com/watch?v=meta-1");

        await vm.MetadataLoadTask;
        vm.CurrentTrackTitle.Should().Be("Fetched Title");
        vm.CurrentTrackArtist.Should().Be("Fetched Artist");
        vm.CurrentTrackThumbnailUrl.Should().Be("https://img.youtube.com/vi/meta-1/0.jpg");
    }

    [Fact]
    public async Task EnqueueRawUrl_WhenMetadataFetchFails_ShouldFallbackToTrackInfo()
    {
        _urlParser.TryExtractVideoId("https://youtube.com/watch?v=meta-null", out Arg.Any<string>()!, out Arg.Any<Uri>()!)
            .Returns(x =>
            {
                x[1] = "meta-null";
                x[2] = new Uri("https://youtube.com/watch?v=meta-null");
                return true;
            });

        _metadataFetcher.FetchMetadataAsync("meta-null", Arg.Any<CancellationToken>())
            .Returns(TrackMetadata.Unknown("meta-null"));

        MainWindowViewModel vm = CreateViewModel();

        vm.EnqueueRawUrl("https://youtube.com/watch?v=meta-null");

        await vm.MetadataLoadTask;
        vm.CurrentTrackTitle.Should().Be("meta-null");
        vm.CurrentTrackArtist.Should().Be("Unknown Artist");
        vm.CurrentTrackThumbnailUrl.Should().BeNull();
    }

    [Fact]
    public async Task AutoSavePlaylist_WhenStorageThrows_ShouldCatchGracefully()
    {
        _urlParser.TryExtractVideoId("https://youtube.com/watch?v=err-save", out Arg.Any<string>()!, out Arg.Any<Uri>()!)
            .Returns(x =>
            {
                x[1] = "err-save";
                x[2] = new Uri("https://youtube.com/watch?v=err-save");
                return true;
            });

        _playlistStorage.When(s => s.SavePlaylistAsync(Arg.Any<Playlist>(), Arg.Any<CancellationToken>()))
            .Do(_ => throw new IOException("Write failed"));

        MainWindowViewModel vm = CreateViewModel();

        Action act = () => vm.EnqueueRawUrl("https://youtube.com/watch?v=err-save");

        act.Should().NotThrow();
        await vm.AutoSaveTask;
    }

    [Fact]
    public void OpenCollaborativePlaylist_WhenUrlSet_ShouldAttemptOpen()
    {
        MainWindowViewModel vm = CreateViewModel();
        vm.CollaborativePlaylistUrl = "https://youtube.com/playlist?list=PLvalid";

        Action act = () => vm.OpenCollaborativePlaylist();

        act.Should().NotThrow();
    }

    [Fact]
    public void CopyWebDropUrl_WhenUrlPresent_ShouldInvokeClipboardAndSetStatus()
    {
        MainWindowViewModel vm = CreateViewModel();
        string? copied = null;
        Action<string> original = MainWindowViewModel.ClipboardAction;
        try
        {
            MainWindowViewModel.ClipboardAction = text => copied = text;
            vm.CopyWebDropUrl();

            copied.Should().Be(vm.WebDropUrl);
            vm.StatusMessage.Should().Be("WebDrop-Link kopiert!");
        }
        finally
        {
            MainWindowViewModel.ClipboardAction = original;
        }
    }

    [Fact]
    public void CopyWebDropUrl_WhenClipboardThrows_ShouldCatchAndSetErrorMessage()
    {
        MainWindowViewModel vm = CreateViewModel();
        Action<string> original = MainWindowViewModel.ClipboardAction;
        try
        {
            MainWindowViewModel.ClipboardAction = _ => throw new InvalidOperationException("STA failure");
            vm.CopyWebDropUrl();

            vm.StatusMessage.Should().Be("Kopieren fehlgeschlagen");
        }
        finally
        {
            MainWindowViewModel.ClipboardAction = original;
        }
    }

    [Fact]
    public void OpenCollaborativePlaylistInBrowser_WhenProcessStarterThrows_ShouldCatchAndSetErrorMessage()
    {
        MainWindowViewModel vm = CreateViewModel();
        vm.CollaborativePlaylistUrl = "https://youtube.com/playlist?list=PLvalid";
        Action<System.Diagnostics.ProcessStartInfo> original = MainWindowViewModel.ProcessStarter;
        try
        {
            MainWindowViewModel.ProcessStarter = _ => throw new System.ComponentModel.Win32Exception("No browser");
            vm.OpenCollaborativePlaylist();

            vm.StatusMessage.Should().Be("Could not open browser for playlist.");
        }
        finally
        {
            MainWindowViewModel.ProcessStarter = original;
        }
    }

    [Fact]
    public async Task VoteToSkipAsync_WhenLocalVoteReachesThresholdDirectly_ShouldSetStatusMessageAndSkip()
    {
        Track track1 = new("threshold-1", new Uri("https://www.youtube.com/watch?v=threshold-1"), "Song 1", "Artist 1", DateTimeOffset.UtcNow);
        Track track2 = new("threshold-2", new Uri("https://www.youtube.com/watch?v=threshold-2"), "Song 2", "Artist 2", DateTimeOffset.UtcNow);
        _group.Playlist.Enqueue(track1);
        _group.Playlist.Enqueue(track2);

        MainWindowViewModel vm = CreateViewModel();

        // Simulate 2 remote peer votes first
        _lanGroupService.SkipVoteReceived += Raise.Event<EventHandler<burroQueue.Infrastructure.Network.Messages.SkipVoteMessage>>(
            this,
            new burroQueue.Infrastructure.Network.Messages.SkipVoteMessage(_group.Id.Value, "threshold-1", Guid.NewGuid(), DateTimeOffset.UtcNow));
        _lanGroupService.SkipVoteReceived += Raise.Event<EventHandler<burroQueue.Infrastructure.Network.Messages.SkipVoteMessage>>(
            this,
            new burroQueue.Infrastructure.Network.Messages.SkipVoteMessage(_group.Id.Value, "threshold-1", Guid.NewGuid(), DateTimeOffset.UtcNow));

        // Now local user votes (3rd vote) -> threshold reached directly in VoteToSkipAsync!
        await vm.VoteToSkipAsync();

        _group.Playlist.CurrentTrack.Should().NotBeNull();
        _group.Playlist.CurrentTrack!.VideoId.Should().Be("threshold-2");
        vm.CurrentTrackTitle.Should().Be("Song 2");
    }

    [Fact]
    public async Task VoteToSkipAsync_WhenPoolTrackIdDiffersFromCurrentTrack_ShouldResetPoolForCurrentTrack()
    {
        Track track1 = new("track-a", new Uri("https://www.youtube.com/watch?v=track-a"), "Song A", "Artist A", DateTimeOffset.UtcNow);
        Track track2 = new("track-b", new Uri("https://www.youtube.com/watch?v=track-b"), "Song B", "Artist B", DateTimeOffset.UtcNow);
        _group.Playlist.Enqueue(track1);
        _group.Playlist.Enqueue(track2);

        MainWindowViewModel vm = CreateViewModel();
        await vm.VoteToSkipAsync();
        vm.SkipVoteStatus.Should().Be("1 / 3 Votes");

        // Force skip to track 2 so track changes
        await vm.SkipAsync();
        _group.Playlist.CurrentTrack!.VideoId.Should().Be("track-b");

        // Now voting again resets pool for track-b
        await vm.VoteToSkipAsync();
        vm.SkipVoteStatus.Should().Be("1 / 3 Votes");
    }

    [Fact]
    public void UiDispatcherRunner_WhenNoDispatcher_ShouldExecuteDirectly()
    {
        bool executed = false;
        MainWindowViewModel.UiDispatcherRunner(() => executed = true);
        executed.Should().BeTrue();
    }
}




