using burroQueue.Domain.Model;
using burroQueue.Infrastructure.Audio;

namespace burroQueue.Infrastructure.Playback;

public sealed class AudioPlayerBackend : IAudioPlayerBackend
{
    private readonly IAudioOutputController _audioOutput;
    private readonly IStreamUriResolver _streamResolver;
    private PlaybackState _currentState = PlaybackState.Stopped;
    private Track? _activeTrack;

    public PlaybackState CurrentState => _currentState;

    public Track? ActiveTrack => _activeTrack;

    public event EventHandler<PlaybackState>? StateChanged;

    public event EventHandler<Track>? TrackPlaybackFinished;

    public AudioPlayerBackend(IAudioOutputController audioOutput, IStreamUriResolver streamResolver)
    {
        _audioOutput = audioOutput ?? throw new ArgumentNullException(nameof(audioOutput));
        _streamResolver = streamResolver ?? throw new ArgumentNullException(nameof(streamResolver));
    }

    public async Task PlayTrackAsync(Track track, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(track);

        _activeTrack = track;
        Uri? streamUri = await _streamResolver.ResolveAudioStreamUriAsync(track.VideoId, cancellationToken).ConfigureAwait(false);

        _currentState = PlaybackState.Playing;
        StateChanged?.Invoke(this, _currentState);
    }

    public void NotifyTrackFinished()
    {
        if (_activeTrack is not null)
        {
            Track finished = _activeTrack;
            _currentState = PlaybackState.Stopped;
            _activeTrack = null;
            StateChanged?.Invoke(this, _currentState);
            TrackPlaybackFinished?.Invoke(this, finished);
        }
    }

    public Task PauseAsync(CancellationToken cancellationToken = default)
    {
        if (_currentState == PlaybackState.Playing)
        {
            _currentState = PlaybackState.Paused;
            StateChanged?.Invoke(this, _currentState);
        }

        return Task.CompletedTask;
    }

    public Task ResumeAsync(CancellationToken cancellationToken = default)
    {
        if (_currentState == PlaybackState.Paused)
        {
            _currentState = PlaybackState.Playing;
            StateChanged?.Invoke(this, _currentState);
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        _currentState = PlaybackState.Stopped;
        _activeTrack = null;
        StateChanged?.Invoke(this, _currentState);
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}
