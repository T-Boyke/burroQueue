using burroQueue.Domain.Model;
using burroQueue.Infrastructure.Audio;

namespace burroQueue.Infrastructure.Playback;

public sealed class AudioPlayerBackend : IAudioPlayerBackend
{
    private readonly IAudioOutputController _audioOutput;
    private PlaybackState _currentState = PlaybackState.Stopped;
    private Track? _activeTrack;

    public PlaybackState CurrentState => _currentState;

    public Track? ActiveTrack => _activeTrack;

    public event EventHandler<PlaybackState>? StateChanged;

    public AudioPlayerBackend(IAudioOutputController audioOutput)
    {
        _audioOutput = audioOutput ?? throw new ArgumentNullException(nameof(audioOutput));
    }

    public Task PlayTrackAsync(Track track, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(track);

        _activeTrack = track;
        _currentState = PlaybackState.Playing;
        StateChanged?.Invoke(this, _currentState);
        return Task.CompletedTask;
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

