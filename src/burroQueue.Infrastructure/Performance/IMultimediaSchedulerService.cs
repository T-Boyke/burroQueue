namespace burroQueue.Infrastructure.Performance;

public interface IMultimediaSchedulerService
{
    bool IsSupported { get; }

    IDisposable RegisterPlaybackThread();
}
