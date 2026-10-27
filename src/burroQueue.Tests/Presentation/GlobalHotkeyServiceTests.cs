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
}

