using burroQueue.Infrastructure.Audio;
using FluentAssertions;
using Xunit;

namespace burroQueue.Tests.Infrastructure;

public sealed class SimdAudioDspProcessorTests
{
    [Fact]
    public void ApplyGainAndSoftClip_ShouldScaleSamplesUsingVector256AndClampPeaks()
    {
        SimdAudioDspProcessor dsp = new();
        float[] samples = new float[64];
        for (int i = 0; i < samples.Length; i++)
        {
            samples[i] = 0.5f;
        }

        dsp.ApplyGainAndSoftClip(samples, 0.5f);

        samples.Should().OnlyContain(s => Math.Abs(s - 0.25f) < 0.0001f);
    }

    [Fact]
    public void ComputeRmsAndNormalizeGain_ShouldMeasureLoudnessAndComputeTargetGain()
    {
        SimdAudioDspProcessor dsp = new();
        float[] quietTrack = Enumerable.Repeat(0.05f, 128).ToArray();
        float[] loudTrack = Enumerable.Repeat(0.75f, 128).ToArray();

        float quietRms = dsp.ComputeRms(quietTrack);
        float loudRms = dsp.ComputeRms(loudTrack);

        quietRms.Should().BeApproximately(0.05f, 0.001f);
        loudRms.Should().BeApproximately(0.75f, 0.001f);

        float gainForQuiet = dsp.ComputeNormalizationGain(quietTrack, targetRms: 0.20f);
        float gainForLoud = dsp.ComputeNormalizationGain(loudTrack, targetRms: 0.20f);

        gainForQuiet.Should().BeGreaterThan(1.0f);
        gainForLoud.Should().BeLessThan(1.0f);
    }

    [Fact]
    public void ComputeEbuR128LinearGain_ShouldCalculateGainFromMeasuredAndTargetLufsAndClampBounds()
    {
        SimdAudioDspProcessor dsp = new();

        double loudTrackGain = dsp.ComputeEbuR128LinearGain(measuredLufs: -8.0, targetLufs: -14.0);
        double quietTrackGain = dsp.ComputeEbuR128LinearGain(measuredLufs: -20.0, targetLufs: -14.0);
        double neutralGain = dsp.ComputeEbuR128LinearGain(measuredLufs: -14.0, targetLufs: -14.0);
        double clampedHigh = dsp.ComputeEbuR128LinearGain(measuredLufs: -40.0, targetLufs: -6.0);
        double clampedLow = dsp.ComputeEbuR128LinearGain(measuredLufs: 0.0, targetLufs: -24.0);

        loudTrackGain.Should().BeApproximately(0.501187, 0.001);
        quietTrackGain.Should().BeApproximately(1.995262, 0.001);
        neutralGain.Should().BeApproximately(1.0, 0.0001);
        clampedHigh.Should().Be(2.0);
        clampedLow.Should().Be(0.15);
    }

    [Fact]
    public void ApplyLoudnessNormalizationToVolume_ShouldScaleMasterVolumeWhenEnabledAndBypassWhenDisabled()
    {
        SimdAudioDspProcessor dsp = new();

        double bypassed = dsp.ApplyLoudnessNormalizationToVolume(0.8, enabled: false, measuredLufs: -8.0, targetLufs: -14.0);
        double normalizedLoud = dsp.ApplyLoudnessNormalizationToVolume(0.8, enabled: true, measuredLufs: -8.0, targetLufs: -14.0);
        double normalizedQuietClamped = dsp.ApplyLoudnessNormalizationToVolume(0.8, enabled: true, measuredLufs: -22.0, targetLufs: -14.0);

        bypassed.Should().Be(0.8);
        normalizedLoud.Should().BeApproximately(0.8 * 0.501187, 0.001);
        normalizedQuietClamped.Should().Be(1.0);
    }
}

