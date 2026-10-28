namespace burroQueue.Domain.Model;

public sealed class Playlist
{
    private readonly List<Track> _items = [];

    public Track? CurrentTrack { get; private set; }

    public IReadOnlyList<Track> QueuedTracks => _items.AsReadOnly();

    public int Count => _items.Count;

    public void Enqueue(Track track)
    {
        ArgumentNullException.ThrowIfNull(track);

        if (CurrentTrack is null && _items.Count == 0)
        {
            CurrentTrack = track;
            return;
        }

        _items.Add(track);
    }

    public Track? AdvanceNext()
    {
        if (_items.Count > 0)
        {
            CurrentTrack = _items[0];
            _items.RemoveAt(0);
        }
        else
        {
            CurrentTrack = null;
        }

        return CurrentTrack;
    }

    public bool Remove(TrackId trackId)
    {
        int index = _items.FindIndex(t => t.Id == trackId);
        if (index >= 0)
        {
            _items.RemoveAt(index);
            return true;
        }

        return false;
    }

    public bool Move(int fromIndex, int toIndex)
    {
        if (fromIndex < 0 || fromIndex >= _items.Count || toIndex < 0 || toIndex >= _items.Count || fromIndex == toIndex)
        {
            return false;
        }

        Track item = _items[fromIndex];
        _items.RemoveAt(fromIndex);
        _items.Insert(toIndex, item);
        return true;
    }

    public bool MoveUp(TrackId trackId)
    {
        int index = _items.FindIndex(t => t.Id == trackId);
        if (index <= 0)
        {
            return false;
        }

        return Move(index, index - 1);
    }

    public bool MoveDown(TrackId trackId)
    {
        int index = _items.FindIndex(t => t.Id == trackId);
        if (index < 0 || index >= _items.Count - 1)
        {
            return false;
        }

        return Move(index, index + 1);
    }


    public void Clear()
    {
        _items.Clear();
        CurrentTrack = null;
    }
}
