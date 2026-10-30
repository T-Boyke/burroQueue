using burroQueue.Infrastructure.Playback;
using Windows.Media.Core;
using Windows.Media.Playback;

namespace burroQueue.Presentation.Services;

[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
public sealed class WpfMediaStreamPlayer : IMediaStreamPlayer
{
    private readonly MediaPlayer _mediaPlayer;
    private MediaSource? _activeMediaSource;
    private TimeSpan? _pendingSeek;
    private double _volume = 0.8;
    private bool _isDisposed;

    public bool IsUsingWindowsMediaFoundation => !_isDisposed;

    public double Volume
    {
        get => _volume;
        set
        {
            double clamped = Math.Clamp(value, 0.0, 1.0);
            _volume = clamped;
            if (!_isDisposed)
            {
                try
                {
                    _mediaPlayer.Volume = clamped;
                }
                catch
                {
                }
            }
        }
    }

    public TimeSpan Position
    {
        get
        {
            if (_isDisposed)
            {
                return TimeSpan.Zero;
            }

            try
            {
                return _mediaPlayer.PlaybackSession.Position;
            }
            catch
            {
                return TimeSpan.Zero;
            }
        }
    }

    public TimeSpan Duration
    {
        get
        {
            if (_isDisposed)
            {
                return TimeSpan.Zero;
            }

            try
            {
                TimeSpan natural = _mediaPlayer.PlaybackSession.NaturalDuration;
                return natural > TimeSpan.Zero && natural != TimeSpan.MaxValue ? natural : TimeSpan.Zero;
            }
            catch
            {
                return TimeSpan.Zero;
            }
        }
    }

    public event EventHandler? MediaEnded;

    public WpfMediaStreamPlayer()
    {
        _mediaPlayer = new MediaPlayer
        {
            AudioCategory = MediaPlayerAudioCategory.Media,
            AutoPlay = false,
            Volume = _volume
        };

        try
        {
            _mediaPlayer.CommandManager.IsEnabled = false;
        }
        catch
        {
        }

        _mediaPlayer.MediaOpened += (sender, _) =>
        {
            try
            {
                if (_pendingSeek is { } target)
                {
                    _pendingSeek = null;
                    sender.PlaybackSession.Position = target;
                }
            }
            catch
            {
            }
        };

        _mediaPlayer.MediaEnded += (_, _) => MediaEnded?.Invoke(this, EventArgs.Empty);
        _mediaPlayer.MediaFailed += (_, e) =>
            App.LogStartup($"WpfMediaStreamPlayer (MediaFoundation) MediaFailed: {e.Error} - {e.ErrorMessage} (0x{e.ExtendedErrorCode?.HResult:X8})");
    }

    public void Open(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        if (_isDisposed)
        {
            return;
        }

        try
        {
            MediaSource? previous = _activeMediaSource;
            MediaSource nextSource = MediaSource.CreateFromUri(uri);
            _activeMediaSource = nextSource;
            _mediaPlayer.Source = nextSource;
            previous?.Dispose();
        }
        catch (Exception ex)
        {
            App.LogStartup($"WpfMediaStreamPlayer Open failed for '{uri}': {ex.Message}");
        }
    }

    public void Play()
    {
        if (_isDisposed)
        {
            return;
        }

        try
        {
            _mediaPlayer.Play();
        }
        catch (Exception ex)
        {
            App.LogStartup($"WpfMediaStreamPlayer Play failed: {ex.Message}");
        }
    }

    public void Pause()
    {
        if (_isDisposed)
        {
            return;
        }

        try
        {
            _mediaPlayer.Pause();
        }
        catch
        {
        }
    }

    public void Seek(TimeSpan position)
    {
        if (_isDisposed)
        {
            return;
        }

        TimeSpan clamped = position < TimeSpan.Zero ? TimeSpan.Zero : position;
        _pendingSeek = clamped;
        try
        {
            if (_mediaPlayer.PlaybackSession.CanSeek)
            {
                _mediaPlayer.PlaybackSession.Position = clamped;
                _pendingSeek = null;
            }
        }
        catch
        {
            // Deferred until MediaOpened
        }
    }

    public void StopPlayback()
    {
        if (_isDisposed)
        {
            return;
        }

        try
        {
            _mediaPlayer.Pause();
            _mediaPlayer.Source = null;
            _activeMediaSource?.Dispose();
            _activeMediaSource = null;
        }
        catch
        {
        }
    }

    public ValueTask DisposeAsync()
    {
        if (!_isDisposed)
        {
            _isDisposed = true;
            try
            {
                _mediaPlayer.Pause();
                _mediaPlayer.Source = null;
                _activeMediaSource?.Dispose();
                _activeMediaSource = null;
                _mediaPlayer.Dispose();
            }
            catch
            {
            }
        }

        return ValueTask.CompletedTask;
    }
}
