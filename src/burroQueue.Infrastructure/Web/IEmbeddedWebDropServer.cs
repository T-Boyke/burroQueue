namespace burroQueue.Infrastructure.Web;

public interface IEmbeddedWebDropServer : IAsyncDisposable
{
    int Port { get; }

    bool IsRunning { get; }

    event EventHandler<string>? LinkDropped;

    Task StartAsync(CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);
}
