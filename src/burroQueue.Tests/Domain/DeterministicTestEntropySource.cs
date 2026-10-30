using burroQueue.Domain.Services;

namespace burroQueue.Tests.Domain;

public sealed class DeterministicTestEntropySource : IEntropySource
{
    private readonly Action<int>? _customPermuter;

    public DeterministicTestEntropySource(Action<int>? customPermuter = null)
    {
        _customPermuter = customPermuter;
    }

    public int GetNextInt32(int fromInclusive, int toExclusive) => fromInclusive;

    public void Shuffle<T>(Span<T> values)
    {
        _customPermuter?.Invoke(values.Length);
        values.Reverse();
    }
}
