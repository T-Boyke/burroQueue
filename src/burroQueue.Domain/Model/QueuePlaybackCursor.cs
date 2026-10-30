namespace burroQueue.Domain.Model;

public readonly record struct QueuePlaybackCursor(
    string ActiveVideoId,
    int QueueIndex,
    TimeSpan PlaybackPosition,
    bool WasPlaying)
{
    public static QueuePlaybackCursor Empty => new(string.Empty, -1, TimeSpan.Zero, false);
}
