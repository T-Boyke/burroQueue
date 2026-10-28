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
        get => _mediaPlayer.Volume;
        set => _mediaPlayer.Volume = Math.Clamp(value, 0.0, 1.0);
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
        _mediaPlayer.Open(uri);
    }

    public void Play()
    {
        _mediaPlayer.Play();
    }

    public void Pause()
    {
        _mediaPlayer.Pause();
    }

    public void StopPlayback()
    {
        _mediaPlayer.Stop();
        _mediaPlayer.Close();
    }

    public ValueTask DisposeAsync()
    {
        if (!_isDisposed)
        {
            _isDisposed = true;
            _mediaPlayer.Stop();
            _mediaPlayer.Close();
        }

        return ValueTask.CompletedTask;
    }
}
