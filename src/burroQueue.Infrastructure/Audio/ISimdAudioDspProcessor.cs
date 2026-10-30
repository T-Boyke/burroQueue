namespace burroQueue.Infrastructure.Audio;

public interface ISimdAudioDspProcessor
{
    bool IsHardwareAccelerated { get; }

    float ComputeRms(ReadOnlySpan<float> samples);

    float ComputeNormalizationGain(ReadOnlySpan<float> samples, float targetRms = 0.18f);

    void ApplyGainAndSoftClip(Span<float> samples, float linearGain);
}

