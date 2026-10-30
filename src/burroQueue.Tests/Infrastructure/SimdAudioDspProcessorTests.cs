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
}

