namespace burroQueue.Infrastructure.Playback;

public interface IMediaStreamPlayer : IAsyncDisposable
{
    double Volume { get; set; }

    TimeSpan Position { get; }

    TimeSpan Duration { get; }

    event EventHandler? MediaEnded;

    void Open(Uri uri);

    void Play();

    void Pause();

    void StopPlayback();

    void Seek(TimeSpan position)
    {
    }
}
