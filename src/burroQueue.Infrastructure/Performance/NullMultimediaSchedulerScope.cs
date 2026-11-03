namespace burroQueue.Infrastructure.Performance;

public sealed class NullMultimediaSchedulerScope : IDisposable
{
    public static NullMultimediaSchedulerScope Instance { get; } = new();

    public void Dispose()
    {
    }
}
