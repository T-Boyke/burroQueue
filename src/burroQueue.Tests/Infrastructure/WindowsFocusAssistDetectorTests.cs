using burroQueue.Infrastructure.Audio;
using burroQueue.Infrastructure.Windows;
using burroQueue.Presentation.Services;
using FluentAssertions;
using Xunit;

namespace burroQueue.Tests.Infrastructure;

public sealed class WindowsFocusAssistDetectorTests
{
    [Theory]
    [InlineData(2, true)]  // QUNS_BUSY
    [InlineData(3, true)]  // QUNS_RUNNING_D3D_FULL_SCREEN
    [InlineData(4, true)]  // QUNS_PRESENTATION_MODE
    [InlineData(6, true)]  // QUNS_QUIET_TIME
    [InlineData(5, false)] // QUNS_ACCEPTS_NOTIFICATIONS
    [InlineData(7, false)] // QUNS_APP
    public void ShouldSuppressNotifications_ShouldRespectShellNotificationState(int rawState, bool expectedSuppressed)
    {
        WindowsFocusAssistDetector detector = new(() => rawState);
        detector.ShouldSuppressNotifications().Should().Be(expectedSuppressed);
    }

    [Fact]
    public void GlobalHotkeyService_WhenBossKeyCtrlAltMTriggered_ShouldRaiseBossKeyMutePressed()
    {
        GlobalHotkeyService hotkey = new();
        bool bossKeyTriggered = false;
        hotkey.BossKeyMutePressed += (_, _) => bossKeyTriggered = true;

        bool handled = hotkey.ProcessMessage(0x0312, new IntPtr(9003));

        handled.Should().BeTrue();
        bossKeyTriggered.Should().BeTrue();
    }

    [Fact]
    public void AudioOutputController_ComputeLogarithmicVolumeStep_ShouldInterpolateSmoothly()
    {
        AudioOutputController controller = new();

        double start = controller.ComputeLogarithmicVolumeStep(0.8, 0.0, 0.0);
        double mid = controller.ComputeLogarithmicVolumeStep(0.8, 0.0, 0.5);
        double end = controller.ComputeLogarithmicVolumeStep(0.8, 0.0, 1.0);

        start.Should().BeApproximately(0.8, 0.001);
        mid.Should().BeLessThan(0.4).And.BeGreaterThan(0.0);
        end.Should().BeApproximately(0.0, 0.001);
    }
}

