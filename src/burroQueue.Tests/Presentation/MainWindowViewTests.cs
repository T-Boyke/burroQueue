using System.ComponentModel;
using System.Windows;
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
using burroQueue.Presentation.Views;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace burroQueue.Tests.Presentation;

public sealed class MainWindowViewTests
{
    private readonly BurroGroup _group = new(GroupId.New(), "View Office");
    private readonly IYouTubeUrlParser _urlParser = Substitute.For<IYouTubeUrlParser>();
    private readonly IYouTubeMetadataFetcher _metadataFetcher = Substitute.For<IYouTubeMetadataFetcher>();
    private readonly ITelephonyManager _telephonyManager = Substitute.For<ITelephonyManager>();
    private readonly ILanGroupService _lanGroupService = Substitute.For<ILanGroupService>();
    private readonly IAudioOutputController _audioController = Substitute.For<IAudioOutputController>();
    private readonly IAppSettingsService _settingsService = Substitute.For<IAppSettingsService>();
    private readonly IEmbeddedWebDropServer _webDropServer = Substitute.For<IEmbeddedWebDropServer>();
    private readonly ILocalNetworkInfoService _networkInfoService = Substitute.For<ILocalNetworkInfoService>();
    private readonly IAudioPlayerBackend _audioPlayerBackend = Substitute.For<IAudioPlayerBackend>();
    private readonly IPlaylistStorageService _playlistStorage = Substitute.For<IPlaylistStorageService>();
    private readonly IGlobalHotkeyService _hotkeyService = Substitute.For<IGlobalHotkeyService>();

    private MainWindowViewModel CreateViewModel()
    {
        _settingsService.CurrentSettings.Returns(new AppSettingsData { ClientDisplayName = "ViewTester" });
        _networkInfoService.GetWebDropUrl(8080).Returns("http://localhost:8080");

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
    public void HandleDragOver_WhenTextPresent_ShouldSetCopyAndHandled()
    {
        IDataObject data = Substitute.For<IDataObject>();
        data.GetDataPresent(DataFormats.Text).Returns(true);

        DragDropEffects effects = DragDropEffects.None;
        bool handled = false;

        MainWindow.HandleDragOver(data, eff => effects = eff, h => handled = h);

        effects.Should().Be(DragDropEffects.Copy);
        handled.Should().BeTrue();
    }

    [Fact]
    public void HandleDragOver_WhenUnicodeTextPresent_ShouldSetCopyAndHandled()
    {
        IDataObject data = Substitute.For<IDataObject>();
        data.GetDataPresent(DataFormats.UnicodeText).Returns(true);

        DragDropEffects effects = DragDropEffects.None;
        bool handled = false;

        MainWindow.HandleDragOver(data, eff => effects = eff, h => handled = h);

        effects.Should().Be(DragDropEffects.Copy);
        handled.Should().BeTrue();
    }

    [Fact]
    public void HandleDragOver_WhenNoTextPresent_ShouldSetNoneAndNotHandled()
    {
        IDataObject data = Substitute.For<IDataObject>();
        data.GetDataPresent(DataFormats.Text).Returns(false);
        data.GetDataPresent(DataFormats.UnicodeText).Returns(false);

        DragDropEffects effects = DragDropEffects.Copy;
        bool handled = false;

        MainWindow.HandleDragOver(data, eff => effects = eff, h => handled = h);

        effects.Should().Be(DragDropEffects.None);
        handled.Should().BeFalse();
    }

    [Fact]
    public void HandleDrop_WhenTextPresent_ShouldExecuteEnqueueCommand()
    {
        MainWindowViewModel vm = CreateViewModel();
        _urlParser.TryExtractVideoId("https://youtube.com/watch?v=drop123", out Arg.Any<string>()!, out Arg.Any<Uri>()!)
            .Returns(x =>
            {
                x[1] = "drop123";
                x[2] = new Uri("https://youtube.com/watch?v=drop123");
                return true;
            });

        IDataObject data = Substitute.For<IDataObject>();
        data.GetDataPresent(DataFormats.UnicodeText).Returns(true);
        data.GetData(DataFormats.UnicodeText).Returns("https://youtube.com/watch?v=drop123");

        bool handled = false;
        MainWindow.HandleDrop(vm, data, h => handled = h);

        handled.Should().BeTrue();
        vm.StatusMessage.Should().Contain("drop123");
    }

    [Fact]
    public void HandleDrop_WhenStandardTextPresent_ShouldExecuteEnqueueCommand()
    {
        MainWindowViewModel vm = CreateViewModel();
        _urlParser.TryExtractVideoId("https://youtube.com/watch?v=drop456", out Arg.Any<string>()!, out Arg.Any<Uri>()!)
            .Returns(x =>
            {
                x[1] = "drop456";
                x[2] = new Uri("https://youtube.com/watch?v=drop456");
                return true;
            });

        IDataObject data = Substitute.For<IDataObject>();
        data.GetDataPresent(DataFormats.UnicodeText).Returns(false);
        data.GetDataPresent(DataFormats.Text).Returns(true);
        data.GetData(DataFormats.Text).Returns("https://youtube.com/watch?v=drop456");

        bool handled = false;
        MainWindow.HandleDrop(vm, data, h => handled = h);

        handled.Should().BeTrue();
        vm.StatusMessage.Should().Contain("drop456");
    }

    [Fact]
    public void HandleDrop_WhenEmptyText_ShouldDoNothing()
    {
        MainWindowViewModel vm = CreateViewModel();
        IDataObject data = Substitute.For<IDataObject>();
        data.GetDataPresent(DataFormats.UnicodeText).Returns(false);
        data.GetDataPresent(DataFormats.Text).Returns(true);
        data.GetData(DataFormats.Text).Returns("   ");

        bool handled = false;
        MainWindow.HandleDrop(vm, data, h => handled = h);

        handled.Should().BeFalse();
    }

    [Fact]
    public void HandleClosing_ShouldCancelAndInvokeHide()
    {
        CancelEventArgs cancelArgs = new();
        bool hideCalled = false;

        MainWindow.HandleClosing(cancelArgs, () => hideCalled = true);

        cancelArgs.Cancel.Should().BeTrue();
        hideCalled.Should().BeTrue();
    }

    [Fact]
    public async Task HandleHotkeyRegistration_ShouldRegisterAndBindEvents()
    {
        MainWindowViewModel vm = CreateViewModel();
        IGlobalHotkeyService hotkeyService = Substitute.For<IGlobalHotkeyService>();
        Window dummyWindow = (Window)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Window));

        MainWindow.HandleHotkeyRegistration(hotkeyService, dummyWindow, vm);

        hotkeyService.Received(1).Register(dummyWindow);

        // Fire hotkey events when Stopped -> triggers PlayAsync
        hotkeyService.PlayPausePressed += Raise.Event<EventHandler>(this, EventArgs.Empty);
        hotkeyService.NextTrackPressed += Raise.Event<EventHandler>(this, EventArgs.Empty);
        await Task.Delay(20);

        // Change state to Playing -> triggers PauseAsync
        vm.PlaybackState = burroQueue.Domain.Model.PlaybackState.Playing;
        hotkeyService.PlayPausePressed += Raise.Event<EventHandler>(this, EventArgs.Empty);
        await Task.Delay(20);
    }

    [Fact]
    public void HandleKeyDown_WhenCtrlVWithValidText_ShouldExecuteEnqueue()
    {
        MainWindowViewModel vm = CreateViewModel();
        _urlParser.TryExtractVideoId("https://youtube.com/watch?v=paste123", out Arg.Any<string>()!, out Arg.Any<Uri>()!)
            .Returns(x =>
            {
                x[1] = "paste123";
                x[2] = new Uri("https://youtube.com/watch?v=paste123");
                return true;
            });

        bool handled = false;
        MainWindow.HandleKeyDown(
            vm,
            System.Windows.Input.Key.V,
            isControl: true,
            getClipboardText: () => "https://youtube.com/watch?v=paste123",
            setHandled: h => handled = h);

        handled.Should().BeTrue();
        vm.StatusMessage.Should().Contain("paste123");
    }

    [Fact]
    public void HandleKeyDown_WhenCtrlVWithEmptyText_ShouldDoNothing()
    {
        MainWindowViewModel vm = CreateViewModel();
        bool handled = false;
        MainWindow.HandleKeyDown(
            vm,
            System.Windows.Input.Key.V,
            isControl: true,
            getClipboardText: () => "   ",
            setHandled: h => handled = h);

        handled.Should().BeFalse();
    }

    [Fact]
    public void HandleKeyDown_WhenKeyIsNotCtrlV_ShouldDoNothing()
    {
        MainWindowViewModel vm = CreateViewModel();
        bool handled = false;

        // V without Ctrl
        MainWindow.HandleKeyDown(
            vm,
            System.Windows.Input.Key.V,
            isControl: false,
            getClipboardText: () => "https://youtube.com",
            setHandled: h => handled = h);
        handled.Should().BeFalse();

        // Ctrl+C instead of Ctrl+V
        MainWindow.HandleKeyDown(
            vm,
            System.Windows.Input.Key.C,
            isControl: true,
            getClipboardText: () => "https://youtube.com",
            setHandled: h => handled = h);
        handled.Should().BeFalse();
    }

    [Fact]
    public void HandleQuickAddKeyDown_WhenKeyIsEnter_ShouldInvokeSubmitAndSetHandled()
    {
        MainWindowViewModel vm = CreateViewModel();
        _urlParser.TryExtractVideoId("https://youtube.com/watch?v=enter123", out Arg.Any<string>()!, out Arg.Any<Uri>()!)
            .Returns(x =>
            {
                x[1] = "enter123";
                x[2] = new Uri("https://youtube.com/watch?v=enter123");
                return true;
            });

        vm.QuickAddUrl = "https://youtube.com/watch?v=enter123";
        bool handled = false;

        MainWindow.HandleQuickAddKeyDown(vm, System.Windows.Input.Key.Enter, h => handled = h);

        handled.Should().BeTrue();
        vm.QuickAddUrl.Should().BeEmpty();
        _group.Playlist.CurrentTrack.Should().NotBeNull();
        _group.Playlist.CurrentTrack!.VideoId.Should().Be("enter123");
    }

    [Fact]
    public void HandleQuickAddKeyDown_WhenKeyIsNotEnter_ShouldNotSetHandled()
    {
        MainWindowViewModel vm = CreateViewModel();
        vm.QuickAddUrl = "https://youtube.com";
        bool handled = false;

        MainWindow.HandleQuickAddKeyDown(vm, System.Windows.Input.Key.Space, h => handled = h);

        handled.Should().BeFalse();
        vm.QuickAddUrl.Should().Be("https://youtube.com");
    }

    [Fact]
    public void HandleQuickAddKeyDown_WhenKeyIsEscape_ShouldClearAndHideQuickAddInput()
    {
        MainWindowViewModel vm = CreateViewModel();
        vm.ShowQuickAddInput();
        vm.QuickAddUrl = "https://youtube.com/watch?v=partial";
        bool handled = false;

        MainWindow.HandleQuickAddKeyDown(vm, System.Windows.Input.Key.Escape, h => handled = h);

        handled.Should().BeTrue();
        vm.QuickAddUrl.Should().BeEmpty();
        vm.IsQuickAddInputVisible.Should().BeFalse();
    }

    [Theory]
    [InlineData("Auto", Wpf.Ui.Controls.WindowBackdropType.Auto)]
    [InlineData("Mica", Wpf.Ui.Controls.WindowBackdropType.Mica)]
    [InlineData("Tabbed", Wpf.Ui.Controls.WindowBackdropType.Tabbed)]
    [InlineData("Acrylic", Wpf.Ui.Controls.WindowBackdropType.Acrylic)]
    [InlineData("None", Wpf.Ui.Controls.WindowBackdropType.None)]
    [InlineData("Unknown", Wpf.Ui.Controls.WindowBackdropType.Mica)]
    [InlineData(null, Wpf.Ui.Controls.WindowBackdropType.Mica)]
    public void ParseBackdropType_ShouldMapStringsToWindowBackdropType(string? input, Wpf.Ui.Controls.WindowBackdropType expected)
    {
        Wpf.Ui.Controls.WindowBackdropType actual = MainWindow.ParseBackdropType(input);
        actual.Should().Be(expected);
    }
}
