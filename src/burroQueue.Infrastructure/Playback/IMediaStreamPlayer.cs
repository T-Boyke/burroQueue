namespace burroQueue.Infrastructure.Playback;

public interface IMediaStreamPlayer : IAsyncDisposable
{
    double Volume { get; set; }

    event EventHandler? MediaEnded;

    void Open(Uri uri);

    void Play();

    void Pause();

    void StopPlayback();
}
