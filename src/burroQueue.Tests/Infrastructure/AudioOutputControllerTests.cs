using burroQueue.Infrastructure.Audio;
using FluentAssertions;
using Xunit;

namespace burroQueue.Tests.Infrastructure;

public sealed class AudioOutputControllerTests
{
    [Fact]
    public void InitialState_ShouldHaveDefaultVolumeAndNotDucked()
    {
        AudioOutputController controller = new();

        controller.CurrentVolume.Should().Be(0.8);
        controller.IsDucked.Should().BeFalse();
    }

    [Theory]
    [InlineData(0.5, true, 0.5, true)]
    [InlineData(1.5, false, 1.0, false)]
    [InlineData(-0.2, true, 0.0, true)]
    public void SetTargetVolume_ShouldClampVolumeAndFireEvent(double target, bool ducked, double expectedVolume, bool expectedDucked)
    {
        AudioOutputController controller = new();
        double? reportedVolume = null;
        controller.VolumeChanged += (_, v) => reportedVolume = v;

        controller.SetTargetVolume(target, ducked);

        controller.CurrentVolume.Should().Be(expectedVolume);
        controller.IsDucked.Should().Be(expectedDucked);
        reportedVolume.Should().Be(expectedVolume);
    }
}
