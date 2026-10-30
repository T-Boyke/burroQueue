namespace burroQueue.Infrastructure.Web;

public interface IEmbeddedWebDropServer : IAsyncDisposable
{
    int Port { get; }

    bool IsRunning { get; }

    Func<string>? SkipVoteStatusProvider { get; set; }

    event EventHandler<LinkDropRequest>? LinkDropped;

    event EventHandler? SkipVoteRequested;

    event EventHandler<WebDropUpvoteRequest>? TrackUpvoteRequested;

    void BroadcastStatusEvent()
    {
    }

    Task StartAsync(CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);
}

