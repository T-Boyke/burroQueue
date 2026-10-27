using burroQueue.Domain.Model;

namespace burroQueue.Infrastructure.Playback;

public interface IAudioPlayerBackend : IAsyncDisposable
{
    PlaybackState CurrentState { get; }

    Track? ActiveTrack { get; }

    event EventHandler<PlaybackState>? StateChanged;

    Task PlayTrackAsync(Track track, CancellationToken cancellationToken = default);

    Task PauseAsync(CancellationToken cancellationToken = default);

    Task ResumeAsync(CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);
}

