using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace burroQueue.Infrastructure.Audio;

public sealed class SimdAudioDspProcessor : ISimdAudioDspProcessor
{
    public bool IsHardwareAccelerated => Vector256.IsHardwareAccelerated;

    public float ComputeRms(ReadOnlySpan<float> samples)
    {
        if (samples.IsEmpty)
        {
            return 0f;
        }

        int i = 0;
        float sumSquares = 0f;

        if (Vector256.IsHardwareAccelerated && samples.Length >= Vector256<float>.Count)
        {
            Vector256<float> acc = Vector256<float>.Zero;
            int simdLimit = samples.Length - (samples.Length % Vector256<float>.Count);
            ref float startRef = ref MemoryMarshal.GetReference(samples);

            for (; i < simdLimit; i += Vector256<float>.Count)
            {
                Vector256<float> vec = Vector256.LoadUnsafe(ref startRef, (nuint)i);
                acc += vec * vec;
            }

            sumSquares = Vector256.Sum(acc);
        }

        for (; i < samples.Length; i++)
        {
            float s = samples[i];
            sumSquares += s * s;
        }

        return MathF.Sqrt(sumSquares / samples.Length);
    }

    public float ComputeNormalizationGain(ReadOnlySpan<float> samples, float targetRms = 0.18f)
    {
        float rms = ComputeRms(samples);
        if (rms <= 0.0001f)
        {
            return 1.0f;
        }

        float desiredGain = targetRms / rms;
        return Math.Clamp(desiredGain, 0.25f, 3.0f);
    }

    public void ApplyGainAndSoftClip(Span<float> samples, float linearGain)
    {
        if (samples.IsEmpty)
        {
            return;
        }

        float clampedGain = Math.Clamp(linearGain, 0.0f, 4.0f);
        int i = 0;

        if (Vector256.IsHardwareAccelerated && samples.Length >= Vector256<float>.Count)
        {
            Vector256<float> gainVec = Vector256.Create(clampedGain);
            Vector256<float> minVec = Vector256.Create(-1.0f);
            Vector256<float> maxVec = Vector256.Create(1.0f);
            int simdLimit = samples.Length - (samples.Length % Vector256<float>.Count);
            ref float startRef = ref MemoryMarshal.GetReference(samples);

            for (; i < simdLimit; i += Vector256<float>.Count)
            {
                Vector256<float> vec = Vector256.LoadUnsafe(ref startRef, (nuint)i);
                Vector256<float> scaled = Vector256.Min(maxVec, Vector256.Max(minVec, vec * gainVec));
                scaled.StoreUnsafe(ref startRef, (nuint)i);
            }
        }

        for (; i < samples.Length; i++)
        {
            samples[i] = Math.Clamp(samples[i] * clampedGain, -1.0f, 1.0f);
        }
    }
}
