using burroQueue.Domain.Model;
using burroQueue.Infrastructure.Audio;

namespace burroQueue.Infrastructure.Playback;

public sealed class AudioPlayerBackend : IAudioPlayerBackend
{
    private readonly IAudioOutputController _audioOutput;
    private readonly IStreamUriResolver _streamResolver;
    private readonly IMediaStreamPlayer? _mediaPlayer;
    private PlaybackState _currentState = PlaybackState.Stopped;
    private Track? _activeTrack;
    private double _currentVolume;

    public PlaybackState CurrentState => _currentState;

    public Track? ActiveTrack => _activeTrack;

    public double CurrentVolume => _currentVolume;

    public TimeSpan Position => _mediaPlayer?.Position ?? TimeSpan.Zero;

    public TimeSpan Duration => _mediaPlayer?.Duration ?? TimeSpan.Zero;

    public event EventHandler<PlaybackState>? StateChanged;

    public event EventHandler<Track>? TrackPlaybackFinished;

    public AudioPlayerBackend(
        IAudioOutputController audioOutput,
        IStreamUriResolver streamResolver,
        IMediaStreamPlayer? mediaPlayer = null)
    {
        _audioOutput = audioOutput ?? throw new ArgumentNullException(nameof(audioOutput));
        _streamResolver = streamResolver ?? throw new ArgumentNullException(nameof(streamResolver));
        _mediaPlayer = mediaPlayer;
        _currentVolume = _audioOutput.CurrentVolume;

        _audioOutput.VolumeChanged += OnVolumeChanged;

        if (_mediaPlayer is not null)
        {
            _mediaPlayer.Volume = _currentVolume;
            _mediaPlayer.MediaEnded += OnMediaEnded;
        }
    }

    private void OnVolumeChanged(object? sender, double newVolume)
    {
        _currentVolume = newVolume;
        if (_mediaPlayer is not null)
        {
            _mediaPlayer.Volume = newVolume;
        }
    }

    private void OnMediaEnded(object? sender, EventArgs e)
    {
        NotifyTrackFinished();
    }

    public async Task PlayTrackAsync(Track track, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(track);

        _activeTrack = track;
        Uri? streamUri = await _streamResolver.ResolveAudioStreamUriAsync(track.VideoId, cancellationToken).ConfigureAwait(false);

        if (_mediaPlayer is not null && streamUri is not null)
        {
            _mediaPlayer.Volume = _currentVolume;
            _mediaPlayer.Open(streamUri);
            _mediaPlayer.Play();
        }

        _currentState = PlaybackState.Playing;
        StateChanged?.Invoke(this, _currentState);
    }

    public async Task PreparePausedAtPositionAsync(Track track, TimeSpan position, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(track);

        _activeTrack = track;
        Uri? streamUri = await _streamResolver.ResolveAudioStreamUriAsync(track.VideoId, cancellationToken).ConfigureAwait(false);

        if (_mediaPlayer is not null && streamUri is not null)
        {
            _mediaPlayer.Volume = _currentVolume;
            _mediaPlayer.Open(streamUri);
            _mediaPlayer.Pause();
            _mediaPlayer.Seek(position);
        }

        _currentState = PlaybackState.Paused;
        StateChanged?.Invoke(this, _currentState);
    }

    public void Seek(TimeSpan position)
    {
        _mediaPlayer?.Seek(position);
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
            _mediaPlayer?.Pause();
            _currentState = PlaybackState.Paused;
            StateChanged?.Invoke(this, _currentState);
        }

        return Task.CompletedTask;
    }

    public Task ResumeAsync(CancellationToken cancellationToken = default)
    {
        if (_currentState == PlaybackState.Paused)
        {
            _mediaPlayer?.Play();
            _currentState = PlaybackState.Playing;
            StateChanged?.Invoke(this, _currentState);
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        _mediaPlayer?.StopPlayback();
        _currentState = PlaybackState.Stopped;
        _activeTrack = null;
        StateChanged?.Invoke(this, _currentState);
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        _audioOutput.VolumeChanged -= OnVolumeChanged;
        if (_mediaPlayer is not null)
        {
            _mediaPlayer.MediaEnded -= OnMediaEnded;
            await _mediaPlayer.DisposeAsync().ConfigureAwait(false);
        }
    }
}

