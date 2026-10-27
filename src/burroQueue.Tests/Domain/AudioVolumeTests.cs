using burroQueue.Domain.Model;
using FluentAssertions;
using Xunit;

namespace burroQueue.Tests.Domain;

public sealed class AudioVolumeTests
{
    [Fact]
    public void CalculateEffectiveLevel_WhenIdle_ShouldReturnNormalLevel()
    {
        AudioVolume volume = new(0.8, 0.2);

        double level = volume.CalculateEffectiveLevel(TelephonyState.Idle);

        level.Should().BeApproximately(0.8, 0.001);
    }

    [Theory]
    [InlineData(TelephonyState.Ringing)]
    [InlineData(TelephonyState.Connecting)]
    [InlineData(TelephonyState.ActiveCall)]
    public void CalculateEffectiveLevel_WhenCallIsActiveOrRinging_ShouldReturnDuckedLevel(TelephonyState state)
    {
        AudioVolume volume = new(0.8, 0.2);

        double level = volume.CalculateEffectiveLevel(state);

        level.Should().BeApproximately(0.16, 0.001);
    }
}
