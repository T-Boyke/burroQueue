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

    public PlaylistReconciliationResult ReconcileRemotePlaylist(IReadOnlyList<Track> remoteTracks)
    {
        ArgumentNullException.ThrowIfNull(remoteTracks);

        Dictionary<string, Track> remoteById = new(StringComparer.Ordinal);
        foreach (Track remote in remoteTracks)
        {
            remoteById.TryAdd(remote.VideoId, remote);
        }

        List<Track> removed = [];
        int updatedCount = 0;

        if (CurrentTrack is not null && remoteById.TryGetValue(CurrentTrack.VideoId, out Track? remoteCurrent))
        {
            if (CurrentTrack.UpdateAttribution(remoteCurrent.SubmittedBy, remoteCurrent.SubmitterAvatarUrl, remoteCurrent.ArtistName))
            {
                updatedCount++;
            }
        }

        for (int i = _items.Count - 1; i >= 0; i--)
        {
            Track existing = _items[i];
            if (!remoteById.TryGetValue(existing.VideoId, out Track? remoteMatch))
            {
                if (existing.IsSyncedFromPlaylist)
                {
                    removed.Add(existing);
                    _items.RemoveAt(i);
                }

                continue;
            }

            if (existing.UpdateAttribution(remoteMatch.SubmittedBy, remoteMatch.SubmitterAvatarUrl, remoteMatch.ArtistName))
            {
                updatedCount++;
            }
        }

        removed.Reverse();

        HashSet<string> presentIds = new(StringComparer.Ordinal);
        if (CurrentTrack is not null)
        {
            presentIds.Add(CurrentTrack.VideoId);
        }

        foreach (Track item in _items)
        {
            presentIds.Add(item.VideoId);
        }

        List<Track> added = [];
        foreach (Track remote in remoteTracks)
        {
            if (presentIds.Add(remote.VideoId))
            {
                Enqueue(remote);
                added.Add(remote);
            }
        }

        return new PlaylistReconciliationResult(added, removed, updatedCount);
    }
}
