using burroQueue.Domain.Model;
using burroQueue.Domain.Services;
using burroQueue.Infrastructure.Performance;

namespace burroQueue.Infrastructure.Audio;

public sealed class SimdTrackVibeAnalyzer : ITrackVibeAnalyzer
{
    private static readonly string[] _disruptiveMarkers =
    [
        "earrape",
        "ear rape",
        "bass boosted",
        "uptempo",
        "terrorcore",
        "extratone",
        "10 hours of silence",
        "screaming"
    ];

    private readonly ISimdAudioDspProcessor _dsp;
    private readonly IProcessEfficiencyManager? _efficiencyManager;

    public SimdTrackVibeAnalyzer(
        ISimdAudioDspProcessor? dsp = null,
        IProcessEfficiencyManager? efficiencyManager = null)
    {
        _dsp = dsp ?? new SimdAudioDspProcessor();
        _efficiencyManager = efficiencyManager;
    }

    public TrackVibeProfile AnalyzeTrackMetadata(string title, string? artist = null)
    {
        return AnalyzeSamplesAndTitle($"{title} {artist}".Trim(), []);
    }

    public TrackVibeProfile AnalyzeSamplesAndTitle(string title, ReadOnlySpan<float> pcmPreviewSamples)
    {
        bool hasDisruptiveKeyword = false;
        if (!string.IsNullOrWhiteSpace(title))
        {
            foreach (string marker in _disruptiveMarkers)
            {
                if (title.Contains(marker, StringComparison.OrdinalIgnoreCase))
                {
                    hasDisruptiveKeyword = true;
                    break;
                }
            }
        }

        if (pcmPreviewSamples.IsEmpty)
        {
            return new TrackVibeProfile(
                RmsEnergy: hasDisruptiveKeyword ? 0.85f : 0.20f,
                ZeroCrossingRate: hasDisruptiveKeyword ? 0.65f : 0.08f,
                CrestFactor: hasDisruptiveKeyword ? 1.1f : 3.5f,
                IsDisruptiveToDeepWork: hasDisruptiveKeyword,
                ClassificationLabel: hasDisruptiveKeyword ? "High-Transient / Disruptive" : "Deep-Work Safe");
        }

        float rms = _dsp.ComputeRms(pcmPreviewSamples);
        float peak = 0f;
        int zeroCrossings = 0;

        for (int i = 0; i < pcmPreviewSamples.Length; i++)
        {
            float abs = MathF.Abs(pcmPreviewSamples[i]);
            if (abs > peak)
            {
                peak = abs;
            }

            if (i > 0 && ((pcmPreviewSamples[i - 1] >= 0f && pcmPreviewSamples[i] < 0f) ||
                          (pcmPreviewSamples[i - 1] < 0f && pcmPreviewSamples[i] >= 0f)))
            {
                zeroCrossings++;
            }
        }

        float zcr = (float)zeroCrossings / pcmPreviewSamples.Length;
        float crest = rms > 0.0001f ? peak / rms : 1.0f;

        // Heavily brickwalled/clipped + high zero-crossing rate or explicit prank/earrape keywords
        bool isDisruptive = hasDisruptiveKeyword || (rms > 0.65f && zcr > 0.45f && crest < 1.35f);

        _efficiencyManager?.TrimMemoryWorkingSet();

        return new TrackVibeProfile(
            rms,
            zcr,
            crest,
            isDisruptive,
            isDisruptive ? "High-Transient / Disruptive" : "Deep-Work Safe");
    }
}

