using burroQueue.Presentation.Services;
using FluentAssertions;
using Xunit;

namespace burroQueue.Tests.Presentation;

public sealed class GlobalHotkeyServiceTests
{
    [Fact]
    public void Dispose_WhenNotRegistered_ShouldBeSafeAndIdempotent()
    {
        GlobalHotkeyService service = new();

        Action act = () =>
        {
            service.Dispose();
            service.Dispose();
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void Register_WithNullWindow_ShouldThrowArgumentNullException()
    {
        GlobalHotkeyService service = new();

        Action act = () => service.Register(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Register_WithWindow_ShouldUseProviderAndRegistrars()
    {
        GlobalHotkeyService service = new();
        IntPtr dummyHwnd = new(0x1234);
        bool hookAttached = false;
        bool hotkeysRegistered = false;

        service.WindowHandleProvider = _ => dummyHwnd;
        service.HwndHookAttacher = (hwnd, _) =>
        {
            hookAttached = hwnd == dummyHwnd;
            return null;
        };
        service.HotkeyRegistrar = hwnd => hotkeysRegistered = hwnd == dummyHwnd;

        System.Windows.Window dummyWindow = (System.Windows.Window)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(System.Windows.Window));
        service.Register(dummyWindow);

        service.WindowHandle.Should().Be(dummyHwnd);
        hookAttached.Should().BeTrue();
        hotkeysRegistered.Should().BeTrue();
    }

    [Fact]
    public void DefaultDelegates_WhenCalledWithZeroHandle_ShouldExecuteSafely()
    {
        GlobalHotkeyService service = new();
        System.Windows.Interop.HwndSource? source = service.HwndHookAttacher(IntPtr.Zero, service.HwndHook);
        source.Should().BeNull();

        Action act = () => service.HotkeyRegistrar(IntPtr.Zero);
        act.Should().NotThrow();
    }

    [Fact]
    public void DefaultDelegates_WhenCalledWithNonZeroHandle_ShouldQueryFromHwndAndReturnNull()
    {
        GlobalHotkeyService service = new();
        System.Windows.Interop.HwndSource? source = service.HwndHookAttacher(new IntPtr(12345), service.HwndHook);
        source.Should().BeNull();
    }

    [Fact]
    public void ProcessMessage_WithPlayPause_ShouldFireEventAndReturnTrue()
    {
        GlobalHotkeyService service = new();
        bool fired = false;
        service.PlayPausePressed += (_, _) => fired = true;

        bool handled = service.ProcessMessage(0x0312, new IntPtr(9001));

        handled.Should().BeTrue();
        fired.Should().BeTrue();
    }

    [Fact]
    public void ProcessMessage_WithNextTrack_ShouldFireEventAndReturnTrue()
    {
        GlobalHotkeyService service = new();
        bool fired = false;
        service.NextTrackPressed += (_, _) => fired = true;

        bool handled = service.ProcessMessage(0x0312, new IntPtr(9002));

        handled.Should().BeTrue();
        fired.Should().BeTrue();
    }

    [Theory]
    [InlineData(0x0312, 1234)]
    [InlineData(0x0001, 9001)]
    [InlineData(0x0000, 0)]
    public void ProcessMessage_WithIrrelevantMessage_ShouldReturnFalseAndNotFire(int msg, int id)
    {
        GlobalHotkeyService service = new();
        bool playFired = false;
        bool nextFired = false;
        service.PlayPausePressed += (_, _) => playFired = true;
        service.NextTrackPressed += (_, _) => nextFired = true;

        bool handled = service.ProcessMessage(msg, new IntPtr(id));

        handled.Should().BeFalse();
        playFired.Should().BeFalse();
        nextFired.Should().BeFalse();
    }

    [Fact]
    public void HwndHook_WithPlayPauseMessage_ShouldSetHandledTrueAndReturnZero()
    {
        GlobalHotkeyService service = new();
        bool playFired = false;
        service.PlayPausePressed += (_, _) => playFired = true;

        bool handled = false;
        IntPtr result = service.HwndHook(IntPtr.Zero, 0x0312, new IntPtr(9001), IntPtr.Zero, ref handled);

        handled.Should().BeTrue();
        result.Should().Be(IntPtr.Zero);
        playFired.Should().BeTrue();
    }

    [Fact]
    public void HwndHook_WithNextTrackMessage_ShouldSetHandledTrueAndReturnZero()
    {
        GlobalHotkeyService service = new();
        bool nextFired = false;
        service.NextTrackPressed += (_, _) => nextFired = true;

        bool handled = false;
        IntPtr result = service.HwndHook(IntPtr.Zero, 0x0312, new IntPtr(9002), IntPtr.Zero, ref handled);

        handled.Should().BeTrue();
        result.Should().Be(IntPtr.Zero);
        nextFired.Should().BeTrue();
    }

    [Fact]
    public void HwndHook_WithIrrelevantMessage_ShouldLeaveHandledFalse()
    {
        GlobalHotkeyService service = new();
        bool handled = false;
        IntPtr result = service.HwndHook(IntPtr.Zero, 0x00A0, IntPtr.Zero, IntPtr.Zero, ref handled);

        handled.Should().BeFalse();
        result.Should().Be(IntPtr.Zero);
    }

    [Fact]
    public void Dispose_WhenWindowHandleSet_ShouldUnregisterAndResetHandle()
    {
        GlobalHotkeyService service = new();
        service.WindowHandle = new IntPtr(12345);

        service.Dispose();

        service.WindowHandle.Should().Be(IntPtr.Zero);
    }
}

