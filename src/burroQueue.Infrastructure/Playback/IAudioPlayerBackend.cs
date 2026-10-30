using burroQueue.Domain.Model;

namespace burroQueue.Infrastructure.Playback;

public interface IAudioPlayerBackend : IAsyncDisposable
{
    PlaybackState CurrentState { get; }

    Track? ActiveTrack { get; }

    double CurrentVolume { get; }

    TimeSpan Position { get; }

    TimeSpan Duration { get; }

    event EventHandler<PlaybackState>? StateChanged;

    event EventHandler<Track>? TrackPlaybackFinished;

    Task PlayTrackAsync(Track track, CancellationToken cancellationToken = default);

    Task PauseAsync(CancellationToken cancellationToken = default);

    Task ResumeAsync(CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);

    Task PreparePausedAtPositionAsync(Track track, TimeSpan position, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    Task PrefetchTrackAsync(Track track, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    void ConfigureLoudnessNormalization(bool enabled, double targetLufs = -14.0)
    {
    }

    void Seek(TimeSpan position)
    {
    }
}
