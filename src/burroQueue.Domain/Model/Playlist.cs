namespace burroQueue.Domain.Model;

public sealed class Playlist
{
    private readonly List<Track> _allTracks = [];

    public int CurrentTrackIndex { get; private set; } = -1;

    public bool IsRepeatAllEnabled { get; set; }

    public Track? CurrentTrack =>
        CurrentTrackIndex >= 0 && CurrentTrackIndex < _allTracks.Count
            ? _allTracks[CurrentTrackIndex]
            : null;

    public IReadOnlyList<Track> AllTracks => _allTracks.AsReadOnly();

    public int TotalCount => _allTracks.Count;

    public IReadOnlyList<Track> QueuedTracks
    {
        get
        {
            if (_allTracks.Count == 0)
            {
                return [];
            }

            if (CurrentTrackIndex < 0)
            {
                return _allTracks.AsReadOnly();
            }

            int start = CurrentTrackIndex + 1;
            return start < _allTracks.Count
                ? _allTracks.GetRange(start, _allTracks.Count - start)
                : [];
        }
    }

    public int Count
    {
        get
        {
            if (_allTracks.Count == 0)
            {
                return 0;
            }

            if (CurrentTrackIndex < 0)
            {
                return _allTracks.Count;
            }

            int remaining = _allTracks.Count - 1 - CurrentTrackIndex;
            return remaining > 0 ? remaining : 0;
        }
    }

    public void Enqueue(Track track)
    {
        ArgumentNullException.ThrowIfNull(track);

        bool wasCurrentNull = CurrentTrack is null;
        _allTracks.Add(track);

        if (wasCurrentNull)
        {
            CurrentTrackIndex = _allTracks.Count - 1;
        }
    }

    public Track? AdvanceNext()
    {
        if (_allTracks.Count == 0)
        {
            CurrentTrackIndex = -1;
            return null;
        }

        if (CurrentTrackIndex < 0)
        {
            CurrentTrackIndex = 0;
            return CurrentTrack;
        }

        if (CurrentTrackIndex + 1 < _allTracks.Count)
        {
            CurrentTrackIndex++;
            return CurrentTrack;
        }

        if (IsRepeatAllEnabled)
        {
            CurrentTrackIndex = 0;
            return CurrentTrack;
        }

        CurrentTrackIndex = _allTracks.Count;
        return null;
    }

    public Track? JumpToIndex(int index)
    {
        if (index < 0 || index >= _allTracks.Count)
        {
            return null;
        }

        CurrentTrackIndex = index;
        return _allTracks[index];
    }

    public Track? JumpToTrack(TrackId trackId)
    {
        int index = _allTracks.FindIndex(t => t.Id == trackId);
        return JumpToIndex(index);
    }

    public bool Remove(TrackId trackId)
    {
        int index = _allTracks.FindIndex(t => t.Id == trackId);
        if (index < 0 || index == CurrentTrackIndex)
        {
            return false;
        }

        _allTracks.RemoveAt(index);
        if (index < CurrentTrackIndex)
        {
            CurrentTrackIndex--;
        }

        return true;
    }

    public bool Move(int fromIndex, int toIndex)
    {
        int upcomingCount = Count;
        if (fromIndex < 0 || fromIndex >= upcomingCount || toIndex < 0 || toIndex >= upcomingCount || fromIndex == toIndex)
        {
            return false;
        }

        int baseOffset = CurrentTrackIndex < 0 ? 0 : CurrentTrackIndex + 1;
        int actualFrom = baseOffset + fromIndex;
        int actualTo = baseOffset + toIndex;

        Track item = _allTracks[actualFrom];
        _allTracks.RemoveAt(actualFrom);
        _allTracks.Insert(actualTo, item);
        return true;
    }

    public bool MoveUp(TrackId trackId)
    {
        int index = _allTracks.FindIndex(t => t.Id == trackId);
        if (index <= 0 || index == CurrentTrackIndex || index == CurrentTrackIndex + 1)
        {
            return false;
        }

        (_allTracks[index - 1], _allTracks[index]) = (_allTracks[index], _allTracks[index - 1]);
        return true;
    }

    public bool MoveDown(TrackId trackId)
    {
        int index = _allTracks.FindIndex(t => t.Id == trackId);
        if (index < 0 || index >= _allTracks.Count - 1 || index == CurrentTrackIndex || index == CurrentTrackIndex - 1)
        {
            return false;
        }

        (_allTracks[index + 1], _allTracks[index]) = (_allTracks[index], _allTracks[index + 1]);
        return true;
    }

    public bool MoveTrackToIndex(TrackId trackId, int targetIndex)
    {
        if (targetIndex < 0 || targetIndex >= _allTracks.Count)
        {
            return false;
        }

        int fromIndex = _allTracks.FindIndex(t => t.Id == trackId);
        if (fromIndex < 0 || fromIndex == targetIndex)
        {
            return false;
        }

        Track item = _allTracks[fromIndex];
        _allTracks.RemoveAt(fromIndex);
        _allTracks.Insert(targetIndex, item);

        if (CurrentTrackIndex == fromIndex)
        {
            CurrentTrackIndex = targetIndex;
        }
        else if (fromIndex < CurrentTrackIndex && targetIndex >= CurrentTrackIndex)
        {
            CurrentTrackIndex--;
        }
        else if (fromIndex > CurrentTrackIndex && targetIndex <= CurrentTrackIndex)
        {
            CurrentTrackIndex++;
        }

        return true;
    }

    public void ReplaceOrderPreservingCursor(IReadOnlyList<Track> orderedTracks)
    {
        ArgumentNullException.ThrowIfNull(orderedTracks);

        TrackId? activeId = CurrentTrack?.Id;
        _allTracks.Clear();
        _allTracks.AddRange(orderedTracks);

        if (_allTracks.Count == 0)
        {
            CurrentTrackIndex = -1;
            return;
        }

        if (activeId is { } id)
        {
            int idx = _allTracks.FindIndex(t => t.Id == id);
            CurrentTrackIndex = idx >= 0 ? idx : 0;
        }
        else if (CurrentTrackIndex > _allTracks.Count)
        {
            CurrentTrackIndex = _allTracks.Count;
        }
    }

    public void Clear()
    {
        _allTracks.Clear();
        CurrentTrackIndex = -1;
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

        for (int i = _allTracks.Count - 1; i >= 0; i--)
        {
            Track existing = _allTracks[i];
            if (i == CurrentTrackIndex)
            {
                if (remoteById.TryGetValue(existing.VideoId, out Track? remoteCurrent) &&
                    existing.UpdateAttribution(remoteCurrent.SubmittedBy, remoteCurrent.SubmitterAvatarUrl, remoteCurrent.ArtistName))
                {
                    updatedCount++;
                }

                continue;
            }

            if (!remoteById.TryGetValue(existing.VideoId, out Track? remoteMatch))
            {
                if (existing.IsSyncedFromPlaylist)
                {
                    removed.Add(existing);
                    _allTracks.RemoveAt(i);
                    if (i < CurrentTrackIndex)
                    {
                        CurrentTrackIndex--;
                    }
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
        foreach (Track item in _allTracks)
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
