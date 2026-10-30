using burroQueue.Domain.Model;

namespace burroQueue.Domain.Services;

public interface ITrackVibeAnalyzer
{
    TrackVibeProfile AnalyzeTrackMetadata(string title, string? artist = null);

    TrackVibeProfile AnalyzeSamplesAndTitle(string title, ReadOnlySpan<float> pcmPreviewSamples);
}

