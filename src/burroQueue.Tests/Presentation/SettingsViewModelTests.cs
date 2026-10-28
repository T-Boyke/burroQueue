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
        vm.CollaborativeUrlValidationText.Should().Contain("Gültiger Einladungslink");
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
}
