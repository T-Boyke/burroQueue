namespace burroQueue.Infrastructure.Network;

public interface IQueueSyncListener : IAsyncDisposable
{
    Task StartAsync(CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);
}

