using System.Security.Cryptography;
using burroQueue.Domain.Services;

namespace burroQueue.Infrastructure.Cryptography;

public sealed class HardwareCsprngEntropySource : IEntropySource
{
    public int GetNextInt32(int fromInclusive, int toExclusive) =>
        RandomNumberGenerator.GetInt32(fromInclusive, toExclusive);

    public void Shuffle<T>(Span<T> values) =>
        RandomNumberGenerator.Shuffle(values);
}
