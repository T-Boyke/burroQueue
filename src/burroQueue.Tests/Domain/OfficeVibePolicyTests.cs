using burroQueue.Domain.Model;
using burroQueue.Domain.Services;
using burroQueue.Infrastructure.Audio;
using FluentAssertions;
using Xunit;

namespace burroQueue.Tests.Domain;

public sealed class OfficeVibePolicyTests
{
    [Fact]
    public void AnalyzeAndCalculateRequiredSkipVotes_WhenTrackIsDisruptiveHardstyleOrPrank_ShouldLowerSkipThresholdToOne()
    {
        SimdTrackVibeAnalyzer analyzer = new();

        // High-frequency clipped square wave simulating harsh/disruptive audio
        float[] harshSamples = new float[256];
        for (int i = 0; i < harshSamples.Length; i++)
        {
            harshSamples[i] = (i % 2 == 0) ? 0.99f : -0.99f;
        }

        TrackVibeProfile harshProfile = analyzer.AnalyzeSamplesAndTitle("Ear Rape 200 BPM Hardstyle Bass Boosted", harshSamples);
        harshProfile.IsDisruptiveToDeepWork.Should().BeTrue();
        OfficeVibePolicy.CalculateRequiredSkipVotes(harshProfile, defaultRequiredVotes: 3).Should().Be(1);

        // Smooth sine wave simulating chill office music
        float[] smoothSamples = new float[256];
        for (int i = 0; i < smoothSamples.Length; i++)
        {
            smoothSamples[i] = MathF.Sin(i * 0.08f) * 0.25f;
        }

        TrackVibeProfile chillProfile = analyzer.AnalyzeSamplesAndTitle("Lofi Hip Hop Beats to Code/Relax To", smoothSamples);
        chillProfile.IsDisruptiveToDeepWork.Should().BeFalse();
        OfficeVibePolicy.CalculateRequiredSkipVotes(chillProfile, defaultRequiredVotes: 3).Should().Be(3);
    }
}
