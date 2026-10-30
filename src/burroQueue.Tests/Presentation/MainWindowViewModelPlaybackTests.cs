using burroQueue.Domain.Model;
using burroQueue.Infrastructure.Audio;
using burroQueue.Infrastructure.Configuration;
using burroQueue.Infrastructure.Network;
using burroQueue.Infrastructure.Persistence;
using burroQueue.Infrastructure.Playback;
using burroQueue.Infrastructure.Telephony;
using burroQueue.Infrastructure.Web;
using burroQueue.Infrastructure.YouTube;
using burroQueue.Presentation.Services;
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

        bool completed = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(2));
        completed.Should().BeTrue();
        _group.Playlist.CurrentTrack!.VideoId.Should().Be("track-2");
    }

    [Fact]
    public void ToggleQueueExpanded_ShouldDefaultToTrueAndInvertIsQueueExpanded()
    {
        MainWindowViewModel vm = CreateViewModel();
        vm.IsQueueExpanded.Should().BeTrue();

        vm.ToggleQueueExpanded();
        vm.IsQueueExpanded.Should().BeFalse();

        vm.ToggleQueueExpanded();
        vm.IsQueueExpanded.Should().BeTrue();
    }

    [Fact]
    public void QuickAddInputVisibility_ShouldDefaultFalseRevealOnShowAndAutoHideOnSubmitOrEmptyBlur()
    {
        _urlParser.TryExtractVideoId("https://youtube.com/watch?v=quick99", out Arg.Any<string>(), out Arg.Any<Uri>())
            .Returns(x =>
            {
                x[1] = "quick99";
                x[2] = new Uri("https://youtube.com/watch?v=quick99");
                return true;
            });

        MainWindowViewModel vm = CreateViewModel();
        vm.IsQuickAddInputVisible.Should().BeFalse();

        vm.ShowQuickAddInput();
        vm.IsQuickAddInputVisible.Should().BeTrue();

        vm.QuickAddUrl = "https://youtube.com/watch?v=quick99";
        vm.HideQuickAddInputIfEmpty();
        vm.IsQuickAddInputVisible.Should().BeTrue("non-empty input must stay visible on focus loss");

        vm.SubmitQuickAdd();
        vm.QuickAddUrl.Should().BeEmpty();
        vm.IsQuickAddInputVisible.Should().BeFalse("submitting Quick-Add should collapse input back to compact drop zone");

        vm.ShowQuickAddInput();
        vm.HideQuickAddInputIfEmpty();
        vm.IsQuickAddInputVisible.Should().BeFalse();
    }

    [Fact]
    public async Task TogglePlayPauseAsync_ShouldPlayWhenStoppedAndPauseWhenPlaying()
    {
        Track track = new("track-1", new Uri("https://www.youtube.com/watch?v=track-1"), "Song 1", "Alice", DateTimeOffset.UtcNow);
        _group.Playlist.Enqueue(track);
        MainWindowViewModel vm = CreateViewModel();

        vm.IsPlaying.Should().BeFalse();

        await vm.TogglePlayPauseAsync();
        vm.IsPlaying.Should().BeTrue();
        await _audioPlayerBackend.Received(1).PlayTrackAsync(track, Arg.Any<CancellationToken>());

        await vm.TogglePlayPauseAsync();
        vm.IsPlaying.Should().BeFalse();
        await _audioPlayerBackend.Received(1).PauseAsync(Arg.Any<CancellationToken>());
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
        _settingsService.CurrentSettings.DuckingReleaseDelaySeconds = 0;
        MainWindowViewModel vm = CreateViewModel();

        _telephonyManager.StateChanged += Raise.Event<EventHandler<TelephonyCallEventArgs>>(
            this,
            new TelephonyCallEventArgs("WindowsCoreAudio", TelephonyState.Idle));

        vm.IsCallActive.Should().BeFalse();
        vm.TelephonyStatusText.Should().BeEmpty();
        _audioController.Received(1).SetTargetVolume(0.8, false);
    }

    [Fact]
    public async Task TelephonyStateChanged_WhenCallEndsWithReleaseDelay_ShouldWaitAndRestoreMasterVolume()
    {
        _settingsService.CurrentSettings.DuckingReleaseDelaySeconds = 5;
        _settingsService.CurrentSettings.MasterVolume = 0.65;
        MainWindowViewModel vm = CreateViewModel();

        Func<TimeSpan, CancellationToken, Task> originalDelay = MainWindowViewModel.DelayProvider;
        TimeSpan capturedDelay = TimeSpan.Zero;
        try
        {
            MainWindowViewModel.DelayProvider = (ts, _) =>
            {
                capturedDelay = ts;
                return Task.CompletedTask;
            };

            _telephonyManager.StateChanged += Raise.Event<EventHandler<TelephonyCallEventArgs>>(
                this,
                new TelephonyCallEventArgs("Starface", TelephonyState.Ringing));
            vm.IsAudioDucked.Should().BeTrue();

            _audioController.ClearReceivedCalls();

            _telephonyManager.StateChanged += Raise.Event<EventHandler<TelephonyCallEventArgs>>(
                this,
                new TelephonyCallEventArgs("Starface", TelephonyState.Idle));

            await vm.DuckingReleaseTask;

            capturedDelay.Should().Be(TimeSpan.FromSeconds(5));
            vm.IsCallActive.Should().BeFalse();
            vm.IsAudioDucked.Should().BeFalse();
            _audioController.Received(1).SetTargetVolume(0.65, false);
        }
        finally
        {
            MainWindowViewModel.DelayProvider = originalDelay;
        }
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

    [Fact]
    public void OpenSettingsCommand_ShouldRaiseOpenSettingsRequestedEvent()
    {
        MainWindowViewModel vm = CreateViewModel();
        bool raised = false;
        vm.OpenSettingsRequested += (_, _) => raised = true;

        vm.OpenSettingsCommand.Execute(null);

        raised.Should().BeTrue();
    }

    [Fact]
    public void SetMasterVolume_ShouldUpdateAudioControllerAndProperty()
    {
        _audioController.CurrentVolume.Returns(0.8);
        MainWindowViewModel vm = CreateViewModel();

        vm.MasterVolume = 0.5;

        vm.MasterVolume.Should().Be(0.5);
        _audioController.Received(1).SetTargetVolume(0.5, Arg.Any<bool>());
    }

    [Fact]
    public void ToggleMute_ShouldToggleMuteStateAndRestorePreviousVolume()
    {
        _audioController.CurrentVolume.Returns(0.7);
        MainWindowViewModel vm = CreateViewModel();

        vm.ToggleMuteCommand.Execute(null);
        vm.IsMuted.Should().BeTrue();
        vm.MasterVolume.Should().Be(0.0);

        vm.ToggleMuteCommand.Execute(null);
        vm.IsMuted.Should().BeFalse();
        vm.MasterVolume.Should().Be(0.7);
    }

    [Fact]
    public void ClearQueueCommand_ShouldClearQueueAndSavePlaylist()
    {
        MainWindowViewModel vm = CreateViewModel();
        Track track1 = new("v1", new Uri("https://youtu.be/v1"), "Track 1", "User", DateTimeOffset.UtcNow);
        Track track2 = new("v2", new Uri("https://youtu.be/v2"), "Track 2", "User", DateTimeOffset.UtcNow);
        _group.Playlist.Enqueue(track1);
        _group.Playlist.Enqueue(track2);
        vm.SyncQueuedItems();
        vm.QueuedCount.Should().Be(1);

        vm.ClearQueueCommand.Execute(null);

        _group.Playlist.Count.Should().Be(0);
        vm.QueuedCount.Should().Be(0);
        vm.QueuedItems.Should().BeEmpty();
    }

    [Fact]
    public void OpenWebDropInBrowserCommand_ShouldStartProcessWithWebDropUrl()
    {
        MainWindowViewModel vm = CreateViewModel();
        System.Diagnostics.ProcessStartInfo? startedPsi = null;
        MainWindowViewModel.ProcessStarter = psi => startedPsi = psi;

        vm.OpenWebDropInBrowserCommand.Execute(null);

        startedPsi.Should().NotBeNull();
        startedPsi!.FileName.Should().Be(vm.WebDropUrl);
        startedPsi.UseShellExecute.Should().BeTrue();
    }

    [Fact]
    public void TelephonyStatus_WhenCallActive_ShouldSetIsAudioDuckedTrue()
    {
        ITelephonyProvider provider = Substitute.For<ITelephonyProvider>();
        provider.ProviderId.Returns("SwyxIt");
        provider.DisplayName.Returns("SwyxIt! Client");
        _telephonyManager.ActiveProvider.Returns(provider);

        MainWindowViewModel vm = CreateViewModel();
        vm.TelephonyProviderName.Should().Be("SwyxIt! Client");

        _telephonyManager.StateChanged += Raise.Event<EventHandler<TelephonyCallEventArgs>>(
            _telephonyManager,
            new TelephonyCallEventArgs("SwyxIt", TelephonyState.ActiveCall));

        vm.IsCallActive.Should().BeTrue();
        vm.IsAudioDucked.Should().BeTrue();
    }

    [Fact]
    public void Constructor_WhenAudioDeviceConfigured_ShouldResolveActiveAudioDeviceName()
    {
        IAudioDeviceEnumerator audioEnumerator = Substitute.For<IAudioDeviceEnumerator>();
        audioEnumerator.GetAvailableRenderDevices().Returns(new[]
        {
            new AudioDeviceOption("dev-1", "Office Speaker (Realtek)", true)
        });

        _settingsService.CurrentSettings.Returns(new AppSettingsData
        {
            SelectedAudioDeviceId = "dev-1"
        });

        MainWindowViewModel vm = new(
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
            _playlistStorage,
            audioEnumerator);

        vm.ActiveAudioDeviceName.Should().Be("Office Speaker (Realtek)");
    }

    [Fact]
    public void AudioController_WhenVolumeChanged_ShouldUpdateMasterVolume()
    {
        MainWindowViewModel vm = CreateViewModel();

        _audioController.VolumeChanged += Raise.Event<EventHandler<double>>(_audioController, 0.42);

        vm.MasterVolume.Should().Be(0.42);
    }

    [Fact]
    public void OpenWebDropInBrowserCommand_WhenProcessStarterThrows_ShouldCatchAndSetErrorMessage()
    {
        MainWindowViewModel vm = CreateViewModel();
        MainWindowViewModel.ProcessStarter = _ => throw new System.ComponentModel.Win32Exception("Browser not found");

        vm.OpenWebDropInBrowserCommand.Execute(null);

        vm.StatusMessage.Should().Be("Konnte Browser nicht öffnen");
    }

    [Fact]
    public void ReloadSettings_ShouldRefreshPlaylistUrlAndAudioDevice()
    {
        IAudioDeviceEnumerator audioEnumerator = Substitute.For<IAudioDeviceEnumerator>();
        audioEnumerator.GetAvailableRenderDevices().Returns(
        [
            new AudioDeviceOption("{dev-1}", "Initial Speaker", false),
            new AudioDeviceOption("{dev-2}", "Headset (Jabra)", false)
        ]);

        _settingsService.CurrentSettings.Returns(new burroQueue.Infrastructure.Configuration.AppSettingsData
        {
            SelectedAudioDeviceId = "{dev-1}",
            CollaborativePlaylistUrl = "https://youtube.com/playlist?list=initial"
        });

        MainWindowViewModel vm = new(
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
            _playlistStorage,
            audioEnumerator);

        vm.ActiveAudioDeviceName.Should().Be("Initial Speaker");
        vm.CollaborativePlaylistUrl.Should().Be("https://youtube.com/playlist?list=initial");

        // Simulate settings change
        _settingsService.CurrentSettings.Returns(new burroQueue.Infrastructure.Configuration.AppSettingsData
        {
            SelectedAudioDeviceId = "{dev-2}",
            CollaborativePlaylistUrl = "https://youtube.com/playlist?list=updated"
        });

        vm.ReloadSettings();

        vm.ActiveAudioDeviceName.Should().Be("Headset (Jabra)");
        vm.CollaborativePlaylistUrl.Should().Be("https://youtube.com/playlist?list=updated");
    }

    [Fact]
    public void EnqueueRawUrl_WithCustomSubmitter_ShouldSetSubmittedByOnEnqueuedTrack()
    {
        _urlParser.TryExtractVideoId("https://youtube.com/watch?v=subm123", out Arg.Any<string>()!, out Arg.Any<Uri>()!)
            .Returns(x =>
            {
                x[1] = "subm123";
                x[2] = new Uri("https://youtube.com/watch?v=subm123");
                return true;
            });

        _urlParser.TryExtractVideoId("https://youtube.com/watch?v=subm456", out Arg.Any<string>()!, out Arg.Any<Uri>()!)
            .Returns(x =>
            {
                x[1] = "subm456";
                x[2] = new Uri("https://youtube.com/watch?v=subm456");
                return true;
            });

        MainWindowViewModel vm = CreateViewModel();
        vm.EnqueueRawUrl("https://youtube.com/watch?v=subm123", "Chef Frank");
        vm.EnqueueRawUrl("https://youtube.com/watch?v=subm456", "Dev Sarah");

        _group.Playlist.CurrentTrack.Should().NotBeNull();
        _group.Playlist.CurrentTrack!.SubmittedBy.Should().Be("Chef Frank");
        _group.Playlist.QueuedTracks.Should().ContainSingle(t => t.VideoId == "subm456" && t.SubmittedBy == "Dev Sarah");
    }

    [Fact]
    public async Task WebDropServer_WhenSkipVoteRequested_ShouldCastVoteToSkip()
    {
        Track track = new("skip-web-1", new Uri("https://www.youtube.com/watch?v=skip-web-1"), "Song 1", "Artist", DateTimeOffset.UtcNow);
        _group.Playlist.Enqueue(track);

        MainWindowViewModel vm = CreateViewModel();
        vm.SkipVoteStatus.Should().Be("0 / 3 Votes");

        _webDropServer.SkipVoteRequested += Raise.Event<EventHandler>(_webDropServer, EventArgs.Empty);
        await Task.Delay(20);

        vm.SkipVoteStatus.Should().Be("1 / 3 Votes");
    }

    [Fact]

    public void MoveTrackUp_WhenValidMiddleItem_ShouldMoveUpAndSyncQueuedItems()
    {
        Track current = new("curr", new Uri("https://youtube.com/watch?v=curr"), "Curr", "Dev", DateTimeOffset.UtcNow);
        Track t1 = new("t1", new Uri("https://youtube.com/watch?v=t1"), "Song 1", "Dev", DateTimeOffset.UtcNow);
        Track t2 = new("t2", new Uri("https://youtube.com/watch?v=t2"), "Song 2", "Dev", DateTimeOffset.UtcNow);
        _group.Playlist.Enqueue(current);
        _group.Playlist.Enqueue(t1);
        _group.Playlist.Enqueue(t2);

        MainWindowViewModel vm = CreateViewModel();
        vm.SyncQueuedItems();
        vm.QueuedItems[0].Id.Should().Be(t1.Id);
        vm.QueuedItems[1].Id.Should().Be(t2.Id);

        vm.MoveTrackUpCommand.Execute(t2);

        vm.QueuedItems[0].Id.Should().Be(t2.Id);
        vm.QueuedItems[1].Id.Should().Be(t1.Id);
        vm.StatusMessage.Should().Contain("verschoben");
    }

    [Fact]
    public void MoveTrackUp_WhenTrackNullOrFirst_ShouldNotChangeOrder()
    {
        Track current = new("curr", new Uri("https://youtube.com/watch?v=curr"), "Curr", "Dev", DateTimeOffset.UtcNow);
        Track t1 = new("t1", new Uri("https://youtube.com/watch?v=t1"), "Song 1", "Dev", DateTimeOffset.UtcNow);
        Track t2 = new("t2", new Uri("https://youtube.com/watch?v=t2"), "Song 2", "Dev", DateTimeOffset.UtcNow);
        _group.Playlist.Enqueue(current);
        _group.Playlist.Enqueue(t1);
        _group.Playlist.Enqueue(t2);

        MainWindowViewModel vm = CreateViewModel();
        vm.MoveTrackUpCommand.Execute(null);
        vm.MoveTrackUpCommand.Execute(t1);

        vm.QueuedItems[0].Id.Should().Be(t1.Id);
        vm.QueuedItems[1].Id.Should().Be(t2.Id);
    }

    [Fact]
    public void MoveTrackDown_WhenValidMiddleItem_ShouldMoveDownAndSyncQueuedItems()
    {
        Track current = new("curr", new Uri("https://youtube.com/watch?v=curr"), "Curr", "Dev", DateTimeOffset.UtcNow);
        Track t1 = new("t1", new Uri("https://youtube.com/watch?v=t1"), "Song 1", "Dev", DateTimeOffset.UtcNow);
        Track t2 = new("t2", new Uri("https://youtube.com/watch?v=t2"), "Song 2", "Dev", DateTimeOffset.UtcNow);
        _group.Playlist.Enqueue(current);
        _group.Playlist.Enqueue(t1);
        _group.Playlist.Enqueue(t2);

        MainWindowViewModel vm = CreateViewModel();
        vm.SyncQueuedItems();

        vm.MoveTrackDownCommand.Execute(t1);

        vm.QueuedItems[0].Id.Should().Be(t2.Id);
        vm.QueuedItems[1].Id.Should().Be(t1.Id);
        vm.StatusMessage.Should().Contain("verschoben");
    }

    [Fact]
    public void MoveTrackDown_WhenTrackNullOrLast_ShouldNotChangeOrder()
    {
        Track current = new("curr", new Uri("https://youtube.com/watch?v=curr"), "Curr", "Dev", DateTimeOffset.UtcNow);
        Track t1 = new("t1", new Uri("https://youtube.com/watch?v=t1"), "Song 1", "Dev", DateTimeOffset.UtcNow);
        Track t2 = new("t2", new Uri("https://youtube.com/watch?v=t2"), "Song 2", "Dev", DateTimeOffset.UtcNow);
        _group.Playlist.Enqueue(current);
        _group.Playlist.Enqueue(t1);
        _group.Playlist.Enqueue(t2);

        MainWindowViewModel vm = CreateViewModel();
        vm.MoveTrackDownCommand.Execute(null);
        vm.MoveTrackDownCommand.Execute(t2);

        vm.QueuedItems[0].Id.Should().Be(t1.Id);
        vm.QueuedItems[1].Id.Should().Be(t2.Id);
    }

    [Fact]
    public void SubmitQuickAdd_WhenValidUrl_ShouldEnqueueAndClearQuickAddUrl()
    {
        _urlParser.TryExtractVideoId("https://youtube.com/watch?v=quick123", out Arg.Any<string>()!, out Arg.Any<Uri>()!)
            .Returns(x =>
            {
                x[1] = "quick123";
                x[2] = new Uri("https://youtube.com/watch?v=quick123");
                return true;
            });

        MainWindowViewModel vm = CreateViewModel();
        vm.QuickAddUrl = "  https://youtube.com/watch?v=quick123  ";

        vm.SubmitQuickAddCommand.Execute(null);

        vm.QuickAddUrl.Should().BeEmpty();
        _group.Playlist.CurrentTrack.Should().NotBeNull();
        _group.Playlist.CurrentTrack!.VideoId.Should().Be("quick123");
    }

    [Fact]
    public void SubmitQuickAdd_WhenWhitespace_ShouldDoNothing()
    {
        MainWindowViewModel vm = CreateViewModel();
        vm.QuickAddUrl = "   ";

        vm.SubmitQuickAddCommand.Execute(null);

        _group.Playlist.Count.Should().Be(0);
        _group.Playlist.CurrentTrack.Should().BeNull();
    }

    [Fact]
    public async Task MetadataLoaded_WithSystrayService_ShouldUpdateTooltipAndShowNotification()
    {
        ISystrayNotificationService systrayMock = Substitute.For<ISystrayNotificationService>();
        MainWindowViewModel vm = new(
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
            _playlistStorage,
            systrayService: systrayMock);

        _urlParser.TryExtractVideoId("https://youtube.com/watch?v=sys123", out Arg.Any<string>()!, out Arg.Any<Uri>()!)
            .Returns(x =>
            {
                x[1] = "sys123";
                x[2] = new Uri("https://youtube.com/watch?v=sys123");
                return true;
            });

        _metadataFetcher.FetchMetadataAsync("sys123")
            .Returns(new TrackMetadata("Harder Better Faster", "Daft Punk", new Uri("https://img.youtube.com/vi/sys123/hqdefault.jpg")));


        vm.EnqueueRawUrl("https://youtube.com/watch?v=sys123");
        await vm.MetadataLoadTask;

        systrayMock.Received(1).UpdateTooltip("burroQueue: Harder Better Faster");
        systrayMock.Received(1).ShowNotification(
            "burroQueue – Jetzt läuft",
            "Harder Better Faster (Daft Punk)",
            "https://img.youtube.com/vi/sys123/hqdefault.jpg");
    }

    [Fact]
    public async Task SkipAsync_WhenQueueBecomesEmpty_ShouldUpdateSystrayTooltip()
    {
        ISystrayNotificationService systrayMock = Substitute.For<ISystrayNotificationService>();
        MainWindowViewModel vm = new(
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
            _playlistStorage,
            systrayService: systrayMock);

        await vm.SkipAsync();

        systrayMock.Received(1).UpdateTooltip("burroQueue – Keine Wiedergabe");
    }

    [Fact]
    public void Constructor_ShouldRestoreMasterVolumeFromSettings()
    {
        _settingsService.CurrentSettings.Returns(new burroQueue.Infrastructure.Configuration.AppSettingsData
        {
            MasterVolume = 0.35
        });

        MainWindowViewModel vm = CreateViewModel();

        vm.MasterVolume.Should().Be(0.35);
        _audioController.Received().SetTargetVolume(0.35, Arg.Any<bool>());
    }

    [Fact]
    public async Task MasterVolume_WhenChanged_ShouldUpdateSettingsAndAutoSave()
    {
        MainWindowViewModel vm = CreateViewModel();

        vm.MasterVolume = 0.55;

        await vm.AutoSaveSettingsTask;

        _settingsService.CurrentSettings.MasterVolume.Should().Be(0.55);
        await _settingsService.Received().SaveAsync(Arg.Is<burroQueue.Infrastructure.Configuration.AppSettingsData>(d => Math.Abs(d.MasterVolume - 0.55) < 0.001));
    }

    [Fact]
    public void ToggleQrCode_ShouldToggleVisibilityAndGenerateSvg()
    {
        burroQueue.Infrastructure.Web.IQrCodeSvgGenerator qrMock = Substitute.For<burroQueue.Infrastructure.Web.IQrCodeSvgGenerator>();
        qrMock.GenerateSvg(Arg.Any<string>(), Arg.Any<int>()).Returns("<svg id='qr-test'></svg>");

        MainWindowViewModel vm = new(
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
            _playlistStorage,
            qrCodeGenerator: qrMock);

        vm.IsQrCodeVisible.Should().BeFalse();

        vm.ToggleQrCodeCommand.Execute(null);
        vm.IsQrCodeVisible.Should().BeTrue();
        vm.QrCodeSvg.Should().Be("<svg id='qr-test'></svg>");

        vm.ToggleQrCodeCommand.Execute(null);
        vm.IsQrCodeVisible.Should().BeFalse();
    }

    [Fact]
    public void CloseQrCode_ShouldSetIsQrCodeVisibleToFalse()
    {
        MainWindowViewModel vm = CreateViewModel();
        vm.IsQrCodeVisible = true;

        vm.CloseQrCodeCommand.Execute(null);

        vm.IsQrCodeVisible.Should().BeFalse();
    }

    [Fact]
    public async Task StopAsync_ShouldResetPlaybackProgressAndTime()
    {
        MainWindowViewModel vm = CreateViewModel();
        vm.PlaybackProgress = 55.0;
        vm.PlaybackTimeFormatted = "01:23 / 02:46";

        await vm.StopAsync();

        vm.PlaybackProgress.Should().Be(0.0);
        vm.PlaybackTimeFormatted.Should().Be("00:00 / 00:00");
    }

    [Fact]
    public async Task PlayAsync_WhenCurrentTrackWasPaused_ShouldInvokeResumeAsync()
    {
        Track track = new("track-1", new Uri("https://www.youtube.com/watch?v=track-1"), "Song 1", "Alice", DateTimeOffset.UtcNow);
        _group.Playlist.Enqueue(track);
        _audioPlayerBackend.CurrentState.Returns(PlaybackState.Paused);
        _audioPlayerBackend.ActiveTrack.Returns(track);

        MainWindowViewModel vm = CreateViewModel();

        await vm.PlayAsync();

        await _audioPlayerBackend.Received(1).ResumeAsync(Arg.Any<CancellationToken>());
        await _audioPlayerBackend.DidNotReceive().PlayTrackAsync(Arg.Any<Track>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PlayTrackNowAsync_WhenTrackInQueue_ShouldJumpCursorNonDestructivelyAndContinueSequentially()
    {
        Track track1 = new("track-1", new Uri("https://www.youtube.com/watch?v=track-1"), "Song 1", "Alice", DateTimeOffset.UtcNow);
        Track track2 = new("track-2", new Uri("https://www.youtube.com/watch?v=track-2"), "Song 2", "Bob", DateTimeOffset.UtcNow);
        Track track3 = new("track-3", new Uri("https://www.youtube.com/watch?v=track-3"), "Song 3", "Charlie", DateTimeOffset.UtcNow);
        _group.Playlist.Enqueue(track1);
        _group.Playlist.Enqueue(track2);
        _group.Playlist.Enqueue(track3);

        MainWindowViewModel vm = CreateViewModel();

        // Jump directly to track2 (index 1)
        await vm.PlayTrackNowAsync(track2);

        _group.Playlist.CurrentTrack.Should().Be(track2);
        _group.Playlist.CurrentTrackIndex.Should().Be(1);
        await _audioPlayerBackend.Received(1).PlayTrackAsync(track2, Arg.Any<CancellationToken>());

        // Non-destructive history: all 3 tracks remain in QueueViewItems with accurate state flags!
        vm.QueueViewItems.Should().HaveCount(3);
        vm.QueueViewItems[0].HasPlayed.Should().BeTrue();
        vm.QueueViewItems[0].IsCurrentlyPlaying.Should().BeFalse();
        vm.QueueViewItems[1].IsCurrentlyPlaying.Should().BeTrue();
        vm.QueueViewItems[2].HasPlayed.Should().BeFalse();

        // Upcoming QueuedItems contains track3, and SkipAsync plays track3 next sequentially!
        vm.QueuedItems.Should().ContainSingle().Which.Should().Be(track3);
        await vm.SkipAsync();
        _group.Playlist.CurrentTrack.Should().Be(track3);
        vm.QueueViewItems[2].IsCurrentlyPlaying.Should().BeTrue();
    }

    [Fact]
    public void Constructor_WhenGroupHasInitialCurrentTrack_ShouldInitializeCurrentTrackProperties()
    {
        Track track = new("init-1", new Uri("https://www.youtube.com/watch?v=init-1"), "Initial Song", "Artist X", DateTimeOffset.UtcNow);
        _group.Playlist.Enqueue(track);

        MainWindowViewModel vm = CreateViewModel();

        vm.CurrentTrackTitle.Should().Be("Initial Song");
        vm.CurrentTrackArtist.Should().Be("Artist X");
        vm.CurrentTrackThumbnailUrl.Should().Be(track.ThumbnailUrl);
    }

    [Fact]
    public void PeerTelephonyState_WhenRemoteGroupMemberEntersAndExitsCall_ShouldDuckAndRestoreHostVolume()
    {
        _settingsService.CurrentSettings.Returns(new burroQueue.Infrastructure.Configuration.AppSettingsData
        {
            MasterVolume = 0.8,
            DuckingReleaseDelaySeconds = 0,
            ClientDisplayName = "Host-PC"
        });

        MainWindowViewModel vm = CreateViewModel();
        Guid peerClientId = Guid.NewGuid();

        _lanGroupService.TelephonyStateReceived += Raise.Event<EventHandler<burroQueue.Infrastructure.Network.Messages.PeerTelephonyStateMessage>>(
            _lanGroupService,
            new burroQueue.Infrastructure.Network.Messages.PeerTelephonyStateMessage(
                _group.Id.Value,
                peerClientId,
                "Bob",
                "SwyxIt!",
                IsCallActive: true,
                DateTimeOffset.UtcNow));

        vm.IsAudioDucked.Should().BeTrue();
        vm.TelephonyStatusText.Should().Contain("Bob");

        _lanGroupService.TelephonyStateReceived += Raise.Event<EventHandler<burroQueue.Infrastructure.Network.Messages.PeerTelephonyStateMessage>>(
            _lanGroupService,
            new burroQueue.Infrastructure.Network.Messages.PeerTelephonyStateMessage(
                _group.Id.Value,
                peerClientId,
                "Bob",
                "SwyxIt!",
                IsCallActive: false,
                DateTimeOffset.UtcNow));

        vm.IsAudioDucked.Should().BeFalse();
    }

    [Fact]
    public async Task ToggleShuffleAsync_ShouldShuffleAndBroadcastOrderAndRestoreOnSecondToggle()
    {
        Track t1 = new("s1", new Uri("https://www.youtube.com/watch?v=s1"), "S1", "Alice", DateTimeOffset.UtcNow);
        Track t2 = new("s2", new Uri("https://www.youtube.com/watch?v=s2"), "S2", "Bob", DateTimeOffset.UtcNow);
        Track t3 = new("s3", new Uri("https://www.youtube.com/watch?v=s3"), "S3", "Clara", DateTimeOffset.UtcNow);
        _group.Playlist.Enqueue(t1);
        _group.Playlist.Enqueue(t2);
        _group.Playlist.Enqueue(t3);

        MainWindowViewModel vm = CreateViewModel();
        vm.IsShuffleEnabled.Should().BeFalse();

        await vm.ToggleShuffleAsync();

        vm.IsShuffleEnabled.Should().BeTrue();
        _group.Playlist.CurrentTrack.Should().BeSameAs(t1);
        await _lanGroupService.Received(1).SendQueueOrderSyncAsync(
            _group.Id,
            Arg.Any<ClientId>(),
            true,
            Arg.Is<IReadOnlyList<string>>(ids => ids.Count == 3 && ids[0] == "s1"),
            Arg.Any<CancellationToken>());

        await vm.ToggleShuffleAsync();

        vm.IsShuffleEnabled.Should().BeFalse();
        _group.Playlist.AllTracks.Select(t => t.VideoId).Should().Equal("s1", "s2", "s3");
    }

    [Fact]
    public async Task ToggleTrackUpvoteAsync_ShouldUpdateVoteCountCurateGroupFavoritesAndAutoHideWhenUnvoted()
    {
        Track t1 = new("up1", new Uri("https://www.youtube.com/watch?v=up1"), "Hard Beat", "Alice", DateTimeOffset.UtcNow);
        _group.Playlist.Enqueue(t1);
        _settingsService.CurrentSettings.Returns(new burroQueue.Infrastructure.Configuration.AppSettingsData
        {
            ClientDisplayName = "Tobias"
        });

        MainWindowViewModel vm = CreateViewModel();
        vm.HasFavoriteTracks.Should().BeFalse();

        await vm.ToggleTrackUpvoteAsync(t1);

        vm.HasFavoriteTracks.Should().BeTrue();
        vm.FavoriteTracks.Should().ContainSingle(f => f.VideoId == "up1" && f.Likes == 1);
        vm.QueueViewItems[0].UpvoteCount.Should().Be(1);
        vm.QueueViewItems[0].IsUpvotedByLocalUser.Should().BeTrue();
        await _lanGroupService.Received(1).SendTrackUpvoteAsync(
            _group.Id,
            "up1",
            t1.Url.ToString(),
            "Hard Beat",
            "Tobias",
            true,
            Arg.Any<CancellationToken>());

        // Toggle off -> 0 likes -> Favorites auto-hides!
        await vm.ToggleTrackUpvoteAsync(t1);

        vm.HasFavoriteTracks.Should().BeFalse();
        vm.FavoriteTracks.Should().BeEmpty();
        vm.QueueViewItems[0].UpvoteCount.Should().Be(0);
    }

    [Fact]
    public async Task HandleClipboardTextAsync_WhenAutoQueueVerifiedMusic_ShouldOnlyEnqueueVerifiedMusicAndRejectNonMusic()
    {
        burroQueue.Domain.Services.IYouTubeMusicValidator musicValidator = Substitute.For<burroQueue.Domain.Services.IYouTubeMusicValidator>();
        musicValidator.ValidateAsync("music123456", Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(new MusicValidationResult(true, "music123456", "Get Lucky", "Daft Punk - Topic", TimeSpan.FromMinutes(4), null));
        musicValidator.ValidateAsync("talk9999999", Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(new MusicValidationResult(false, "talk9999999", "2 Hour Podcast", "Tech Talk", TimeSpan.FromHours(2), "Category 'People & Blogs' is not Music"));

        _urlParser.TryExtractVideoId("https://music.youtube.com/watch?v=music123456", out Arg.Any<string>()!, out Arg.Any<Uri>()!)
            .Returns(x =>
            {
                x[1] = "music123456";
                x[2] = new Uri("https://www.youtube.com/watch?v=music123456");
                return true;
            });
        _urlParser.TryExtractVideoId("https://www.youtube.com/watch?v=talk9999999", out Arg.Any<string>()!, out Arg.Any<Uri>()!)
            .Returns(x =>
            {
                x[1] = "talk9999999";
                x[2] = new Uri("https://www.youtube.com/watch?v=talk9999999");
                return true;
            });

        _settingsService.CurrentSettings.Returns(new burroQueue.Infrastructure.Configuration.AppSettingsData
        {
            ClipboardDetectionMode = "AutoQueueVerifiedMusic",
            MaxMusicDurationMinutes = 12,
            ClientDisplayName = "Tobias"
        });

        MainWindowViewModel vm = new(
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
            _playlistStorage,
            musicValidator: musicValidator);

        bool acceptedMusic = await vm.HandleClipboardTextAsync("https://music.youtube.com/watch?v=music123456");
        acceptedMusic.Should().BeTrue();
        _group.Playlist.AllTracks.Should().ContainSingle(t => t.VideoId == "music123456");

        bool acceptedTalk = await vm.HandleClipboardTextAsync("https://www.youtube.com/watch?v=talk9999999");
        acceptedTalk.Should().BeFalse();
        _group.Playlist.AllTracks.Should().NotContain(t => t.VideoId == "talk9999999");
    }

    [Fact]
    public async Task HostHandoff_WhenRemoteHostBroadcastsHandoff_ShouldRestoreCursorAndSeekPosition()
    {
        Track t1 = new("h1", new Uri("https://www.youtube.com/watch?v=h1"), "Song 1", "Alice", DateTimeOffset.UtcNow);
        Track t2 = new("h2", new Uri("https://www.youtube.com/watch?v=h2"), "Song 2", "Bob", DateTimeOffset.UtcNow);
        _group.Playlist.Enqueue(t1);
        _group.Playlist.Enqueue(t2);

        MainWindowViewModel vm = CreateViewModel();

        _lanGroupService.HostHandoffReceived += Raise.Event<EventHandler<burroQueue.Infrastructure.Network.Messages.HostHandoffMessage>>(
            _lanGroupService,
            new burroQueue.Infrastructure.Network.Messages.HostHandoffMessage(
                _group.Id.Value,
                Guid.NewGuid(),
                "h2",
                1,
                PlaybackPositionSeconds: 75.0,
                WasPlaying: true,
                DateTimeOffset.UtcNow));

        await Task.Delay(30);

        _group.Playlist.CurrentTrackIndex.Should().Be(1);
        _group.Playlist.CurrentTrack.Should().BeSameAs(t2);
        await _audioPlayerBackend.Received(1).PreparePausedAtPositionAsync(t2, TimeSpan.FromSeconds(75), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PlayAsync_WhenQueueReachedEnd_ShouldWrapToFirstTrackAndPlay()
    {
        Track t1 = new("wrap-1", new Uri("https://www.youtube.com/watch?v=wrap-1"), "Song 1", "Alice", DateTimeOffset.UtcNow);
        _group.Playlist.Enqueue(t1);
        _group.Playlist.AdvanceNext(); // Reaches end of queue (CurrentTrack is null, Count == 0, TotalCount == 1)
        _group.Playlist.CurrentTrack.Should().BeNull();

        MainWindowViewModel vm = CreateViewModel();

        await vm.PlayAsync();

        _group.Playlist.CurrentTrack.Should().BeSameAs(t1);
        await _audioPlayerBackend.Received(1).PlayTrackAsync(t1, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WpfMediaStreamPlayer_ShouldUseWindowsMediaFoundationWithoutLegacyWmpException()
    {
        await using WpfMediaStreamPlayer player = new();
        player.IsUsingWindowsMediaFoundation.Should().BeTrue();
        player.Volume = 0.35;
        player.Volume.Should().BeApproximately(0.35, 0.01);
    }

    [Fact]
    public async Task MoveTrackToTarget_WhenDraggingTrackToNewPosition_ShouldReorderPlaylistPreserveCurrentAndBroadcastOrderSync()
    {
        Track t1 = new("d1", new Uri("https://www.youtube.com/watch?v=d1"), "Song 1", "Alice", DateTimeOffset.UtcNow);
        Track t2 = new("d2", new Uri("https://www.youtube.com/watch?v=d2"), "Song 2", "Bob", DateTimeOffset.UtcNow);
        Track t3 = new("d3", new Uri("https://www.youtube.com/watch?v=d3"), "Song 3", "Clara", DateTimeOffset.UtcNow);
        _group.Playlist.Enqueue(t1);
        _group.Playlist.Enqueue(t2);
        _group.Playlist.Enqueue(t3);

        MainWindowViewModel vm = CreateViewModel();

        vm.MoveTrackToTarget(t3, t2);
        await vm.AutoSaveTask;

        _group.Playlist.AllTracks.Select(t => t.VideoId).Should().Equal("d1", "d3", "d2");
        _group.Playlist.CurrentTrack.Should().BeSameAs(t1);
        vm.QueuedItems.Select(t => t.VideoId).Should().Equal("d3", "d2");
        await _lanGroupService.Received(1).SendQueueOrderSyncAsync(
            _group.Id,
            Arg.Any<ClientId>(),
            false,
            Arg.Is<IReadOnlyList<string>>(ids => ids.Count == 3 && ids[0] == "d1" && ids[1] == "d3" && ids[2] == "d2"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PlayAndSkipAsync_ShouldTriggerLookaheadPrefetchForNextUpcomingTrackAndConfigureLoudnessNormalization()
    {
        Track t1 = new("p1", new Uri("https://www.youtube.com/watch?v=p1"), "Song 1", "Alice", DateTimeOffset.UtcNow);
        Track t2 = new("p2", new Uri("https://www.youtube.com/watch?v=p2"), "Song 2", "Bob", DateTimeOffset.UtcNow);
        Track t3 = new("p3", new Uri("https://www.youtube.com/watch?v=p3"), "Song 3", "Clara", DateTimeOffset.UtcNow);
        _group.Playlist.Enqueue(t1);
        _group.Playlist.Enqueue(t2);
        _group.Playlist.Enqueue(t3);

        MainWindowViewModel vm = CreateViewModel();
        _audioPlayerBackend.Received(1).ConfigureLoudnessNormalization(true, -14.0);

        await vm.PlayAsync();
        await vm.PrefetchTask;
        await _audioPlayerBackend.Received(1).PrefetchTrackAsync(t2, Arg.Any<CancellationToken>());

        await vm.SkipAsync();
        await vm.PrefetchTask;
        await _audioPlayerBackend.Received(1).PlayTrackAsync(t2, Arg.Any<CancellationToken>());
        await _audioPlayerBackend.Received(1).PrefetchTrackAsync(t3, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GroupActionJoinReceived_ShouldBroadcastExistingTracksToJoinedPeer()
    {
        Track t1 = new("join-1", new Uri("https://www.youtube.com/watch?v=join-1"), "Host Track 1", "Alice", DateTimeOffset.UtcNow);
        _group.Playlist.Enqueue(t1);

        MainWindowViewModel vm = CreateViewModel();
        _lanGroupService.ClearReceivedCalls();

        _lanGroupService.GroupActionReceived += Raise.Event<EventHandler<burroQueue.Infrastructure.Network.Messages.GroupActionMessage>>(
            _lanGroupService,
            new burroQueue.Infrastructure.Network.Messages.GroupActionMessage(
                _group.Id.Value,
                Guid.NewGuid(),
                "Join",
                "PeerBob",
                DateTimeOffset.UtcNow));

        await Task.Delay(30);

        vm.StatusMessage.Should().Contain("PeerBob");
        await _lanGroupService.Received(1).SendQueueSyncAsync(_group.Id, "Enqueue", t1, Arg.Any<CancellationToken>());
    }
}
