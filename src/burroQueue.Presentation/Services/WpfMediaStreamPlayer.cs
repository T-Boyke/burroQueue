using System.Windows.Media;
using burroQueue.Infrastructure.Playback;

namespace burroQueue.Presentation.Services;

[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
public sealed class WpfMediaStreamPlayer : IMediaStreamPlayer
{
    private readonly MediaPlayer _mediaPlayer;
    private bool _isDisposed;

    public double Volume
    {
        get => _mediaPlayer.Dispatcher.CheckAccess() ? _mediaPlayer.Volume : _mediaPlayer.Dispatcher.Invoke(() => _mediaPlayer.Volume);
        set
        {
            double clamped = Math.Clamp(value, 0.0, 1.0);
            if (_mediaPlayer.Dispatcher.CheckAccess())
            {
                _mediaPlayer.Volume = clamped;
            }
            else
            {
                _mediaPlayer.Dispatcher.Invoke(() => _mediaPlayer.Volume = clamped);
            }
        }
    }

    public event EventHandler? MediaEnded;

    public WpfMediaStreamPlayer()
    {
        _mediaPlayer = new MediaPlayer();
        _mediaPlayer.MediaEnded += (_, e) => MediaEnded?.Invoke(this, e);
    }

    public void Open(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        if (_mediaPlayer.Dispatcher.CheckAccess())
        {
            _mediaPlayer.Open(uri);
        }
        else
        {
            _mediaPlayer.Dispatcher.Invoke(() => _mediaPlayer.Open(uri));
        }
    }

    public void Play()
    {
        if (_mediaPlayer.Dispatcher.CheckAccess())
        {
            _mediaPlayer.Play();
        }
        else
        {
            _mediaPlayer.Dispatcher.Invoke(() => _mediaPlayer.Play());
        }
    }

    public void Pause()
    {
        if (_mediaPlayer.Dispatcher.CheckAccess())
        {
            _mediaPlayer.Pause();
        }
        else
        {
            _mediaPlayer.Dispatcher.Invoke(() => _mediaPlayer.Pause());
        }
    }

    public void StopPlayback()
    {
        if (_mediaPlayer.Dispatcher.CheckAccess())
        {
            _mediaPlayer.Stop();
            _mediaPlayer.Close();
        }
        else
        {
            _mediaPlayer.Dispatcher.Invoke(() =>
            {
                _mediaPlayer.Stop();
                _mediaPlayer.Close();
            });
        }
    }

    public ValueTask DisposeAsync()
    {
        if (!_isDisposed)
        {
            _isDisposed = true;
            if (_mediaPlayer.Dispatcher.CheckAccess())
            {
                _mediaPlayer.Stop();
                _mediaPlayer.Close();
            }
            else
            {
                _mediaPlayer.Dispatcher.Invoke(() =>
                {
                    _mediaPlayer.Stop();
                    _mediaPlayer.Close();
                });
            }
        }

        return ValueTask.CompletedTask;
    }
}
