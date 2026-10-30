namespace burroQueue.Domain.Services;

public interface IEntropySource
{
    int GetNextInt32(int fromInclusive, int toExclusive);

    void Shuffle<T>(Span<T> values);
}
