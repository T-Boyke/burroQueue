namespace burroQueue.Domain.Model;

public sealed class Playlist
{
    private readonly Queue<Track> _queue = new();

    public Track? CurrentTrack { get; private set; }

    public IReadOnlyCollection<Track> QueuedTracks => _queue;

    public int Count => _queue.Count;

    public void Enqueue(Track track)
    {
        ArgumentNullException.ThrowIfNull(track);

        if (CurrentTrack is null && _queue.Count == 0)
        {
            CurrentTrack = track;
            return;
        }

        _queue.Enqueue(track);
    }

    public Track? AdvanceNext()
    {
        CurrentTrack = _queue.Count > 0 ? _queue.Dequeue() : null;
        return CurrentTrack;
    }

    public void Clear()
    {
        _queue.Clear();
        CurrentTrack = null;
    }
}

