namespace burroQueue.Infrastructure.Audio;

public interface ISimdAudioDspProcessor
{
    bool IsHardwareAccelerated { get; }

    float ComputeRms(ReadOnlySpan<float> samples);

    float ComputeNormalizationGain(ReadOnlySpan<float> samples, float targetRms = 0.18f);

    void ApplyGainAndSoftClip(Span<float> samples, float linearGain);

    double ComputeEbuR128LinearGain(double measuredLufs, double targetLufs = -14.0);

    double ApplyLoudnessNormalizationToVolume(double masterVolume, bool enabled, double measuredLufs, double targetLufs = -14.0);
}

