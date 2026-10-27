using burroQueue.Domain.Model;
using burroQueue.Infrastructure.Audio;
using burroQueue.Infrastructure.Configuration;
using burroQueue.Infrastructure.Network;
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
            _audioPlayerBackend);
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
}

