namespace burroQueue.Infrastructure.Web;

public interface IEmbeddedWebDropServer : IAsyncDisposable
{
    int Port { get; }

    bool IsRunning { get; }

    Func<string>? SkipVoteStatusProvider { get; set; }

    Func<string>? PlaybackStateProvider { get; set; }

    Func<int>? VolumeProvider { get; set; }

    event EventHandler<LinkDropRequest>? LinkDropped;

    event EventHandler? SkipVoteRequested;

    event EventHandler? PlayRequested;

    event EventHandler? PauseRequested;

    event EventHandler? SkipRequested;

    event EventHandler<int>? VolumeChangeRequested;

    event EventHandler<string>? TrackSelectRequested;

    event EventHandler<WebDropUpvoteRequest>? TrackUpvoteRequested;

    void BroadcastStatusEvent()
    {
    }

    Task StartAsync(CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);
}

