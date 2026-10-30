using burroQueue.Domain.Model;
using burroQueue.Infrastructure.Audio;

namespace burroQueue.Infrastructure.Playback;

public sealed class AudioPlayerBackend : IAudioPlayerBackend
{
    private readonly IAudioOutputController _audioOutput;
    private readonly IStreamUriResolver _streamResolver;
    private readonly IMediaStreamPlayer? _mediaPlayer;
    private readonly ISimdAudioDspProcessor _dspProcessor;
    private PlaybackState _currentState = PlaybackState.Stopped;
    private Track? _activeTrack;
    private double _currentVolume;
    private bool _loudnessNormalizationEnabled;
    private double _targetLoudnessLufs = -14.0;
    private double _activeTrackMeasuredLufs = -14.0;

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
        IMediaStreamPlayer? mediaPlayer = null,
        ISimdAudioDspProcessor? dspProcessor = null)
    {
        _audioOutput = audioOutput ?? throw new ArgumentNullException(nameof(audioOutput));
        _streamResolver = streamResolver ?? throw new ArgumentNullException(nameof(streamResolver));
        _mediaPlayer = mediaPlayer;
        _dspProcessor = dspProcessor ?? new SimdAudioDspProcessor();
        _currentVolume = _audioOutput.CurrentVolume;

        _audioOutput.VolumeChanged += OnVolumeChanged;

        if (_mediaPlayer is not null)
        {
            _mediaPlayer.Volume = ComputeEffectivePlayerVolume();
            _mediaPlayer.MediaEnded += OnMediaEnded;
        }
    }

    public void ConfigureLoudnessNormalization(bool enabled, double targetLufs = -14.0)
    {
        _loudnessNormalizationEnabled = enabled;
        _targetLoudnessLufs = Math.Clamp(targetLufs, -24.0, -6.0);
        if (_mediaPlayer is not null)
        {
            _mediaPlayer.Volume = ComputeEffectivePlayerVolume();
        }
    }

    private double ComputeEffectivePlayerVolume() =>
        _dspProcessor.ApplyLoudnessNormalizationToVolume(
            _currentVolume,
            _loudnessNormalizationEnabled,
            _activeTrackMeasuredLufs,
            _targetLoudnessLufs);

    private void OnVolumeChanged(object? sender, double newVolume)
    {
        _currentVolume = newVolume;
        if (_mediaPlayer is not null)
        {
            _mediaPlayer.Volume = ComputeEffectivePlayerVolume();
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
        Task<Uri?> streamTask = _streamResolver.ResolveAudioStreamUriAsync(track.VideoId, cancellationToken);
        Task<double> loudnessTask = _streamResolver.ResolveTrackLoudnessLufsAsync(track.VideoId, cancellationToken);

        await Task.WhenAll(streamTask, loudnessTask).ConfigureAwait(false);
        Uri? streamUri = await streamTask.ConfigureAwait(false);
        _activeTrackMeasuredLufs = await loudnessTask.ConfigureAwait(false);

        if (_mediaPlayer is not null && streamUri is not null)
        {
            _mediaPlayer.Volume = ComputeEffectivePlayerVolume();
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
        Task<Uri?> streamTask = _streamResolver.ResolveAudioStreamUriAsync(track.VideoId, cancellationToken);
        Task<double> loudnessTask = _streamResolver.ResolveTrackLoudnessLufsAsync(track.VideoId, cancellationToken);

        await Task.WhenAll(streamTask, loudnessTask).ConfigureAwait(false);
        Uri? streamUri = await streamTask.ConfigureAwait(false);
        _activeTrackMeasuredLufs = await loudnessTask.ConfigureAwait(false);

        if (_mediaPlayer is not null && streamUri is not null)
        {
            _mediaPlayer.Volume = ComputeEffectivePlayerVolume();
            _mediaPlayer.Open(streamUri);
            _mediaPlayer.Pause();
            _mediaPlayer.Seek(position);
        }

        _currentState = PlaybackState.Paused;
        StateChanged?.Invoke(this, _currentState);
    }

    public async Task PrefetchTrackAsync(Track track, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(track);

        try
        {
            Task<Uri?> streamTask = _streamResolver.ResolveAudioStreamUriAsync(track.VideoId, cancellationToken);
            Task<double> loudnessTask = _streamResolver.ResolveTrackLoudnessLufsAsync(track.VideoId, cancellationToken);
            await Task.WhenAll(streamTask, loudnessTask).ConfigureAwait(false);
        }
        catch
        {
            // Best-effort lookahead prefetch
        }
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

