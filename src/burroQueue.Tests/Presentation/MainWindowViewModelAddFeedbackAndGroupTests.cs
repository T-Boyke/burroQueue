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
using burroQueue.Presentation.Services;
using burroQueue.Presentation.ViewModels;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace burroQueue.Tests.Presentation;

public sealed class MainWindowViewModelAddFeedbackAndGroupTests : IDisposable
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
    private readonly ISystrayNotificationService _systrayService;
    private readonly IYouTubePlaylistReader _playlistReader;
    private readonly Action<Action> _originalDispatcher;

    public MainWindowViewModelAddFeedbackAndGroupTests()
    {
        _group = new BurroGroup(GroupId.New(), "Feedback Group");
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
        _systrayService = Substitute.For<ISystrayNotificationService>();
        _playlistReader = Substitute.For<IYouTubePlaylistReader>();

        _settingsService.CurrentSettings.Returns(new AppSettingsData
        {
            ClientDisplayName = "TestUser",
            AutoOpenCollaborativePlaylistOnJoin = false,
            CollaborativePlaylistUrl = "https://www.youtube.com/playlist?list=PLtest&jct=tok",
            HasJoinedCollaborativePlaylist = false,
            EnableWebDropServer = false
        });

        _networkInfoService.GetWebDropUrl(8080).Returns("http://192.168.1.10:8080");

        _originalDispatcher = MainWindowViewModel.UiDispatcherRunner;
        MainWindowViewModel.UiDispatcherRunner = action => action();
    }

    public void Dispose()
    {
        MainWindowViewModel.UiDispatcherRunner = _originalDispatcher;
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
            _playlistStorage,
            systrayService: _systrayService,
            playlistReader: _playlistReader);
    }

    [Fact]
    public void Constructor_WhenCollaborativePlaylistExistsAndNotJoined_ShouldShowFirstJoinPrompt()
    {
        MainWindowViewModel vm = CreateViewModel();

        vm.IsFirstJoinPromptVisible.Should().BeTrue();
        vm.IsWebDropActive.Should().BeFalse();
        vm.NetworkStatusText.Should().Contain("WebDrop aus");
    }

    [Fact]
    public void EnqueueRawUrl_WithValidUrl_ShouldTriggerVisualFeedback()
    {
        Uri uri = new("https://www.youtube.com/watch?v=dQw4w9WgXcQ");
        _urlParser.TryExtractVideoId("https://www.youtube.com/watch?v=dQw4w9WgXcQ", out Arg.Any<string>(), out Arg.Any<Uri>())
            .Returns(x =>
            {
                x[1] = "dQw4w9WgXcQ";
                x[2] = uri;
                return true;
            });

        MainWindowViewModel vm = CreateViewModel();

        vm.EnqueueRawUrl("https://www.youtube.com/watch?v=dQw4w9WgXcQ");

        vm.IsAddFeedbackVisible.Should().BeTrue();
        vm.LastAddedFeedbackText.Should().Contain("dQw4w9WgXcQ");
        _group.Playlist.CurrentTrack.Should().NotBeNull();
        _group.Playlist.CurrentTrack!.VideoId.Should().Be("dQw4w9WgXcQ");
    }

    [Fact]
    public void OpenYouTubeInviteAndDismissPrompt_ShouldHidePromptAndMarkSetting()
    {
        Action<System.Diagnostics.ProcessStartInfo> original = MainWindowViewModel.ProcessStarter;
        try
        {
            MainWindowViewModel.ProcessStarter = _ => { };
            MainWindowViewModel vm = CreateViewModel();

            vm.OpenYouTubeInviteAndDismissPrompt();

            vm.IsFirstJoinPromptVisible.Should().BeFalse();
            _settingsService.CurrentSettings.HasJoinedCollaborativePlaylist.Should().BeTrue();
        }
        finally
        {
            MainWindowViewModel.ProcessStarter = original;
        }
    }

    [Fact]
    public void DismissFirstJoinPrompt_ShouldHidePromptAndMarkSetting()
    {
        MainWindowViewModel vm = CreateViewModel();

        vm.DismissFirstJoinPrompt();

        vm.IsFirstJoinPromptVisible.Should().BeFalse();
        _settingsService.CurrentSettings.HasJoinedCollaborativePlaylist.Should().BeTrue();
    }

    [Fact]
    public void GroupActionReceived_WhenActionIsJoin_ShouldNotifySystray()
    {
        MainWindowViewModel vm = CreateViewModel();

        GroupActionMessage joinAction = new(
            _group.Id.Value,
            Guid.NewGuid(),
            "Join",
            "Klaus",
            DateTimeOffset.UtcNow);

        _lanGroupService.GroupActionReceived += Raise.Event<EventHandler<GroupActionMessage>>(_lanGroupService, joinAction);

        _systrayService.Received(1).ShowNotification("burroQueue – Neues Gruppenmitglied", Arg.Is<string>(s => s.Contains("Klaus")));
        vm.StatusMessage.Should().Contain("Klaus");
    }

    [Fact]
    public async Task ImportPlaylistFromYouTubeAsync_WhenReaderReturnsTracks_ShouldEnqueueAndTriggerFeedback()
    {
        List<Track> tracks =
        [
            new Track("v1", new Uri("https://www.youtube.com/watch?v=v1"), "Song 1", "Artist 1", DateTimeOffset.UtcNow),
            new Track("v2", new Uri("https://www.youtube.com/watch?v=v2"), "Song 2", "Artist 2", DateTimeOffset.UtcNow)
        ];

        _playlistReader.ReadPlaylistTracksAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(tracks);

        MainWindowViewModel vm = CreateViewModel();

        await vm.ImportPlaylistFromYouTubeAsync("https://www.youtube.com/playlist?list=PL12345");

        _group.Playlist.CurrentTrack.Should().NotBeNull();
        _group.Playlist.CurrentTrack!.VideoId.Should().Be("v1");
        vm.QueuedCount.Should().Be(1);
        vm.IsAddFeedbackVisible.Should().BeTrue();
        vm.LastAddedFeedbackText.Should().Contain("2 Songs aus Playlist importiert");
    }

    [Fact]
    public void Group_WhenSwitchedOrRenamedExternally_ShouldUpdateViewModelGroupNameAndSyncQueue()
    {
        MainWindowViewModel vm = CreateViewModel();
        vm.GroupName.Should().Be("Test Group");

        _group.SwitchGroup(GroupId.New(), "Marketing Lounge");

        vm.GroupName.Should().Be("Marketing Lounge");
    }
}

