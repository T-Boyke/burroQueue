using burroQueue.Infrastructure.Audio;
using burroQueue.Infrastructure.Configuration;
using burroQueue.Infrastructure.Telephony;
using burroQueue.Infrastructure.Windows;
using burroQueue.Presentation.ViewModels;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace burroQueue.Tests.Presentation;

public sealed class SettingsViewModelTests
{
    private readonly IAppSettingsService _settingsService;
    private readonly ITelephonyManager _telephonyManager;
    private readonly IWindowsAutostartService _autostartService;
    private readonly IAudioDeviceEnumerator _audioDeviceEnumerator;

    public SettingsViewModelTests()
    {
        _settingsService = Substitute.For<IAppSettingsService>();
        _telephonyManager = Substitute.For<ITelephonyManager>();
        _autostartService = Substitute.For<IWindowsAutostartService>();
        _audioDeviceEnumerator = Substitute.For<IAudioDeviceEnumerator>();

        _settingsService.CurrentSettings.Returns(new AppSettingsData
        {
            SelectedTelephonyProviderId = "WindowsCoreAudio",
            SelectedAudioDeviceId = "device-1",
            CollaborativePlaylistUrl = "https://www.youtube.com/playlist?list=PL1234567890&jct=token123",
            AutoOpenCollaborativePlaylistOnJoin = true,
            ClientDisplayName = "Alice"
        });

        _autostartService.IsAutostartEnabled.Returns(true);

        _audioDeviceEnumerator.GetAvailableRenderDevices().Returns(
        [
            new AudioDeviceOption("device-1", "Speakers", IsDefault: true),
            new AudioDeviceOption("device-2", "Headphones", IsDefault: false)
        ]);

        ITelephonyProvider provider1 = Substitute.For<ITelephonyProvider>();
        provider1.ProviderId.Returns("WindowsCoreAudio");
        _telephonyManager.AvailableProviders.Returns([provider1]);
    }

    [Fact]
    public void Constructor_ShouldInitializePropertiesFromServices()
    {
        SettingsViewModel vm = new(
            _settingsService,
            _telephonyManager,
            _autostartService,
            _audioDeviceEnumerator);

        vm.SelectedProviderId.Should().Be("WindowsCoreAudio");
        vm.SelectedAudioDeviceId.Should().Be("device-1");
        vm.CollaborativePlaylistUrl.Should().Be("https://www.youtube.com/playlist?list=PL1234567890&jct=token123");
        vm.AutoOpenCollaborativePlaylistOnJoin.Should().BeTrue();
        vm.ClientDisplayName.Should().Be("Alice");
        vm.IsAutostartEnabled.Should().BeTrue();
        vm.AvailableAudioDevices.Should().HaveCount(2);
        vm.AvailableTelephonyProviders.Should().HaveCount(1);
    }

    [Fact]
    public async Task SaveSettingsAsync_WithValidSettings_ShouldPersistAndSwitchProvider()
    {
        SettingsViewModel vm = new(
            _settingsService,
            _telephonyManager,
            _autostartService,
            _audioDeviceEnumerator);

        vm.SelectedProviderId = "NullTelephony";
        vm.SelectedAudioDeviceId = "device-2";
        vm.CollaborativePlaylistUrl = "https://www.youtube.com/playlist?list=PLvalidPlaylistId123&jct=tok123";
        vm.AutoOpenCollaborativePlaylistOnJoin = false;
        vm.ClientDisplayName = "Bob";
        vm.IsAutostartEnabled = false;

        await vm.SaveSettingsAsync();

        await _settingsService.Received(1).SaveAsync(Arg.Is<AppSettingsData>(d =>
            d.SelectedTelephonyProviderId == "NullTelephony" &&
            d.SelectedAudioDeviceId == "device-2" &&
            d.CollaborativePlaylistUrl == "https://www.youtube.com/playlist?list=PLvalidPlaylistId123&jct=tok123" &&
            !d.AutoOpenCollaborativePlaylistOnJoin &&
            d.ClientDisplayName == "Bob"));

        await _telephonyManager.Received(1).SwitchProviderAsync("NullTelephony", Arg.Any<CancellationToken>());
        _autostartService.Received(1).SetAutostart(false);
        vm.StatusMessage.Should().Be("Einstellungen gespeichert!");
    }

    [Fact]
    public async Task SaveSettingsAsync_WithEmptyPlaylistUrl_ShouldSaveNullPlaylistUrl()
    {
        SettingsViewModel vm = new(
            _settingsService,
            _telephonyManager,
            _autostartService,
            _audioDeviceEnumerator);

        vm.CollaborativePlaylistUrl = "   ";

        await vm.SaveSettingsAsync();

        await _settingsService.Received(1).SaveAsync(Arg.Is<AppSettingsData>(d =>
            d.CollaborativePlaylistUrl == null));
        vm.StatusMessage.Should().Be("Einstellungen gespeichert!");
    }

    [Fact]
    public async Task SaveSettingsAsync_WithInvalidPlaylistUrl_ShouldSetStatusMessageAndNotSave()
    {
        SettingsViewModel vm = new(
            _settingsService,
            _telephonyManager,
            _autostartService,
            _audioDeviceEnumerator);

        vm.CollaborativePlaylistUrl = "not-a-valid-youtube-playlist-url";

        await vm.SaveSettingsAsync();

        await _settingsService.DidNotReceive().SaveAsync(Arg.Any<AppSettingsData>());
        vm.StatusMessage.Should().StartWith("Ungültiger Link");
    }

    [Fact]
    public async Task SaveSettingsAsync_WithEmptyDisplayName_ShouldFallbackToUserName()
    {
        SettingsViewModel vm = new(
            _settingsService,
            _telephonyManager,
            _autostartService,
            _audioDeviceEnumerator);

        vm.ClientDisplayName = "   ";

        await vm.SaveSettingsAsync();

        await _settingsService.Received(1).SaveAsync(Arg.Is<AppSettingsData>(d =>
            d.ClientDisplayName == Environment.UserName));
    }

    [Fact]
    public void Constructor_WhenDiagnosticsProvided_ShouldPopulateDiagnosticsProperties()
    {
        burroQueue.Infrastructure.Playback.IStreamUriResolver resolver = Substitute.For<burroQueue.Infrastructure.Playback.IStreamUriResolver>();
        resolver.IsYtDlpAvailable.Returns(true);

        burroQueue.Infrastructure.Web.IEmbeddedWebDropServer webDrop = Substitute.For<burroQueue.Infrastructure.Web.IEmbeddedWebDropServer>();
        webDrop.Port.Returns(8080);

        burroQueue.Infrastructure.Network.ILocalNetworkInfoService netInfo = Substitute.For<burroQueue.Infrastructure.Network.ILocalNetworkInfoService>();
        netInfo.GetWebDropUrl(8080).Returns("http://192.168.2.109:8080");

        SettingsViewModel vm = new(
            _settingsService,
            _telephonyManager,
            _autostartService,
            _audioDeviceEnumerator,
            resolver,
            webDrop,
            netInfo);

        vm.AudioEngineStatus.Should().Contain("yt-dlp");
        vm.WebDropEndpointInfo.Should().Be("http://192.168.2.109:8080");
        vm.LanMulticastInfo.Should().Contain("239.255.42.99");
    }

    [Fact]
    public void CollaborativePlaylistUrl_WhenValidInviteLink_ShouldValidateAsTrue()
    {
        SettingsViewModel vm = new(
            _settingsService,
            _telephonyManager,
            _autostartService,
            _audioDeviceEnumerator);

        vm.CollaborativePlaylistUrl = "https://www.youtube.com/playlist?list=PL1234567890&jct=abcdef123";

        vm.IsCollaborativeUrlValid.Should().BeTrue();
        vm.CollaborativeUrlValidationText.Should().Contain("Gültige Playlist");
    }

    [Fact]
    public void CollaborativePlaylistUrl_WhenInvalidLink_ShouldValidateAsFalse()
    {
        SettingsViewModel vm = new(
            _settingsService,
            _telephonyManager,
            _autostartService,
            _audioDeviceEnumerator);

        vm.CollaborativePlaylistUrl = "https://google.com";

        vm.IsCollaborativeUrlValid.Should().BeFalse();
        vm.CollaborativeUrlValidationText.Should().Contain("Ungültig");
    }

    [Fact]
    public async Task SaveSettingsAsync_WithEnableWebDropServer_ShouldPersistSetting()
    {
        SettingsViewModel vm = new(
            _settingsService,
            _telephonyManager,
            _autostartService,
            _audioDeviceEnumerator);

        vm.EnableWebDropServer = true;

        await vm.SaveSettingsAsync();

        await _settingsService.Received(1).SaveAsync(Arg.Is<AppSettingsData>(d => d.EnableWebDropServer));
    }

    [Fact]
    public void CreateNewGroup_WithValidName_ShouldUpdateGroupAndClearInput()
    {
        burroQueue.Domain.Model.BurroGroup group = new(burroQueue.Domain.Model.GroupId.New(), "Initial Group");
        burroQueue.Infrastructure.Network.ILanGroupService lan = Substitute.For<burroQueue.Infrastructure.Network.ILanGroupService>();

        SettingsViewModel vm = new(
            _settingsService,
            _telephonyManager,
            _autostartService,
            _audioDeviceEnumerator,
            group: group,
            lanGroupService: lan);

        vm.NewGroupName = "Marketing Lounge";

        vm.CreateNewGroup();

        group.Name.Should().Be("Marketing Lounge");
        vm.CurrentGroupName.Should().Be("Marketing Lounge");
        vm.NewGroupName.Should().BeEmpty();
        vm.StatusMessage.Should().Contain("Marketing Lounge");
    }

    [Fact]
    public void IsStarfaceSelected_WhenSelectedProviderIsStarface_ShouldReturnTrue()
    {
        SettingsViewModel vm = new(
            _settingsService,
            _telephonyManager,
            _autostartService,
            _audioDeviceEnumerator);

        vm.IsStarfaceSelected.Should().BeFalse();

        vm.SelectedProviderId = "Starface";
        vm.IsStarfaceSelected.Should().BeTrue();

        vm.SelectedProviderId = "SwyxIt";
        vm.IsStarfaceSelected.Should().BeFalse();
    }

    [Fact]
    public async Task SaveSettingsAsync_WithDuckingDelayAndPlaylistAutoSync_ShouldClampAndPersistValues()
    {
        SettingsViewModel vm = new(
            _settingsService,
            _telephonyManager,
            _autostartService,
            _audioDeviceEnumerator);

        vm.DuckingReleaseDelaySeconds = 8;
        vm.AutoSyncCollaborativePlaylist = true;
        vm.AutoSyncPlaylistIntervalSeconds = 45;
        vm.SelectedWindowBackdrop = "Tabbed";
        vm.UseSystemAccentColor = true;

        await vm.SaveSettingsAsync();

        await _settingsService.Received(1).SaveAsync(Arg.Is<AppSettingsData>(d =>
            d.DuckingReleaseDelaySeconds == 8 &&
            d.AutoSyncCollaborativePlaylist &&
            d.AutoSyncPlaylistIntervalSeconds == 45 &&
            d.WindowBackdrop == "Tabbed" &&
            d.UseSystemAccentColor));
        vm.AvailableWindowBackdrops.Should().ContainInOrder("Auto", "Mica", "Tabbed", "Acrylic", "None");
    }

    [Fact]
    public async Task EnableWebDropServer_WhenToggled_ShouldImmediatelyStartAndStopServer()
    {
        burroQueue.Infrastructure.Web.IEmbeddedWebDropServer webDrop = Substitute.For<burroQueue.Infrastructure.Web.IEmbeddedWebDropServer>();
        webDrop.Port.Returns(8080);

        burroQueue.Infrastructure.Network.ILocalNetworkInfoService netInfo = Substitute.For<burroQueue.Infrastructure.Network.ILocalNetworkInfoService>();
        netInfo.GetWebDropUrl(8080).Returns("http://192.168.1.42:8080");

        SettingsViewModel vm = new(
            _settingsService,
            _telephonyManager,
            _autostartService,
            _audioDeviceEnumerator,
            webDropServer: webDrop,
            networkInfo: netInfo);

        webDrop.IsRunning.Returns(false);
        vm.EnableWebDropServer = true;
        await vm.WebDropToggleTask;

        await webDrop.Received(1).StartAsync(Arg.Any<CancellationToken>());
        vm.WebDropEndpointInfo.Should().Be("http://192.168.1.42:8080");

        webDrop.IsRunning.Returns(true);
        vm.EnableWebDropServer = false;
        await vm.WebDropToggleTask;

        await webDrop.Received(1).StopAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AboutSection_ShouldPopulateAuthorStatsYtDlpVersionAndLaunchExternalLinks()
    {
        burroQueue.Infrastructure.Playback.IStreamUriResolver streamResolver = Substitute.For<burroQueue.Infrastructure.Playback.IStreamUriResolver>();
        streamResolver.IsYtDlpAvailable.Returns(true);
        streamResolver.GetYtDlpVersionAsync(Arg.Any<CancellationToken>()).Returns("2026.10.01");

        burroQueue.Infrastructure.Diagnostics.IGitHubRepoStatsClient statsClient = Substitute.For<burroQueue.Infrastructure.Diagnostics.IGitHubRepoStatsClient>();
        statsClient.FetchSnapshotAsync(Arg.Any<CancellationToken>()).Returns(new burroQueue.Infrastructure.Diagnostics.GitHubAboutSnapshot(
            "Tobias Boyke",
            "@T-Boyke",
            new Uri("https://github.com/T-Boyke"),
            new Uri("https://avatars.githubusercontent.com/u/37149164?v=4&s=72"),
            new Uri("https://github.com/T-Boyke/burroQueue"),
            PublicRepos: 18,
            Followers: 25,
            Stars: 77,
            LicenseSpdx: "AGPL-3.0",
            AppVersion: "1.1.0",
            IsLive: true));

        List<string> openedUrls = [];
        Action<System.Diagnostics.ProcessStartInfo> originalStarter = SettingsViewModel.ProcessStarter;
        SettingsViewModel.ProcessStarter = psi => openedUrls.Add(psi.FileName);
        try
        {
            SettingsViewModel vm = new(
                _settingsService,
                _telephonyManager,
                _autostartService,
                _audioDeviceEnumerator,
                streamResolver: streamResolver,
                gitHubStatsClient: statsClient);

            await vm.AboutLoadTask;

            vm.TechStackSummary.Should().Contain(".NET 11").And.Contain("UnoCSS 66.12.0");
            vm.YtDlpVersionText.Should().Contain("2026.10.01");
            vm.AuthorDisplayName.Should().Be("Tobias Boyke");
            vm.AuthorHandle.Should().Be("@T-Boyke");
            vm.AuthorStatsSummary.Should().Contain("18 Repos").And.Contain("25 Followers").And.Contain("77");

            vm.OpenYtDlpRepositoryCommand.Execute(null);
            vm.OpenAuthorProfileCommand.Execute(null);
            vm.OpenProjectRepositoryCommand.Execute(null);
            vm.ReportBugOnGitHubCommand.Execute(null);
            vm.RequestFeatureOnGitHubCommand.Execute(null);

            openedUrls.Should().Contain("https://github.com/yt-dlp/yt-dlp");
            openedUrls.Should().Contain("https://github.com/T-Boyke");
            openedUrls.Should().Contain("https://github.com/T-Boyke/burroQueue");
            openedUrls.Should().Contain(u => u.Contains("issues/new?template=bug_report.yml", StringComparison.Ordinal));
            openedUrls.Should().Contain(u => u.Contains("issues/new?template=feature_request.yml", StringComparison.Ordinal));
        }
        finally
        {
            SettingsViewModel.ProcessStarter = originalStarter;
        }
    }

    [Fact]
    public async Task ScanGroupsAsync_ShouldSendActiveDiscoveryProbeAndAnnounceBeacon()
    {
        burroQueue.Domain.Model.BurroGroup group = new(burroQueue.Domain.Model.GroupId.New(), "Office A");
        burroQueue.Infrastructure.Network.ILanGroupService lan = Substitute.For<burroQueue.Infrastructure.Network.ILanGroupService>();

        SettingsViewModel vm = new(
            _settingsService,
            _telephonyManager,
            _autostartService,
            _audioDeviceEnumerator,
            group: group,
            lanGroupService: lan);

        await vm.ScanGroupsAsync();

        await lan.Received(1).SendDiscoveryProbeAsync(
            Arg.Any<burroQueue.Domain.Model.ClientId>(),
            "Alice",
            Arg.Any<CancellationToken>());
    }
}
