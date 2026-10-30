using burroQueue.Domain.Model;

namespace burroQueue.Domain.Services;

public sealed class QueueShuffleEngine
{
    private const int MaxHistoryEntries = 16;

    private readonly List<Track> _canonicalOrder = [];
    private readonly Queue<string> _recentPlayedVideoIds = new();

    public bool IsShuffleEnabled { get; private set; }

    public void RecordPlayed(string videoId)
    {
        if (string.IsNullOrWhiteSpace(videoId))
        {
            return;
        }

        string trimmed = videoId.Trim();
        _recentPlayedVideoIds.Enqueue(trimmed);
        while (_recentPlayedVideoIds.Count > MaxHistoryEntries)
        {
            _recentPlayedVideoIds.Dequeue();
        }
    }

    public void EnableShuffle(Playlist playlist, IEntropySource entropySource)
    {
        ArgumentNullException.ThrowIfNull(playlist);
        ArgumentNullException.ThrowIfNull(entropySource);

        if (!IsShuffleEnabled)
        {
            _canonicalOrder.Clear();
            _canonicalOrder.AddRange(playlist.AllTracks);
        }

        IsShuffleEnabled = true;
        if (playlist.AllTracks.Count <= 1)
        {
            return;
        }

        Track? pinnedActive = playlist.CurrentTrack;
        List<Track> pool = new(playlist.AllTracks.Count);
        foreach (Track item in playlist.AllTracks)
        {
            if (pinnedActive is null || item.Id != pinnedActive.Id)
            {
                pool.Add(item);
            }
        }

        Track[] poolArray = [.. pool];
        entropySource.Shuffle(poolArray.AsSpan());

        // Anti-repeat boundary guard: prevent last played track from becoming the immediate next track
        string? lastBoundaryId = _recentPlayedVideoIds.Count > 0
            ? _recentPlayedVideoIds.Last()
            : pinnedActive?.VideoId;

        if (poolArray.Length > 1 &&
            !string.IsNullOrEmpty(lastBoundaryId) &&
            string.Equals(poolArray[0].VideoId, lastBoundaryId, StringComparison.Ordinal))
        {
            int swapIdx = -1;
            for (int i = poolArray.Length - 1; i >= 1; i--)
            {
                if (!string.Equals(poolArray[i].VideoId, lastBoundaryId, StringComparison.Ordinal))
                {
                    swapIdx = i;
                    break;
                }
            }

            if (swapIdx > 0)
            {
                (poolArray[0], poolArray[swapIdx]) = (poolArray[swapIdx], poolArray[0]);
            }
        }

        List<Track> reordered = new(playlist.AllTracks.Count);
        if (pinnedActive is not null)
        {
            reordered.Add(pinnedActive);
        }

        reordered.AddRange(poolArray);
        playlist.ReplaceOrderPreservingCursor(reordered);
    }

    public void DisableShuffle(Playlist playlist)
    {
        ArgumentNullException.ThrowIfNull(playlist);

        IsShuffleEnabled = false;
        if (_canonicalOrder.Count == 0 || playlist.AllTracks.Count == 0)
        {
            _canonicalOrder.Clear();
            return;
        }

        Dictionary<TrackId, Track> currentById = new(playlist.AllTracks.Count);
        foreach (Track t in playlist.AllTracks)
        {
            currentById[t.Id] = t;
        }

        List<Track> restored = new(playlist.AllTracks.Count);
        HashSet<TrackId> placed = [];

        foreach (Track canonical in _canonicalOrder)
        {
            if (currentById.TryGetValue(canonical.Id, out Track? stillPresent) && placed.Add(stillPresent.Id))
            {
                restored.Add(stillPresent);
            }
        }

        // Append any tracks added after shuffle was enabled
        foreach (Track t in playlist.AllTracks)
        {
            if (placed.Add(t.Id))
            {
                restored.Add(t);
            }
        }

        _canonicalOrder.Clear();
        playlist.ReplaceOrderPreservingCursor(restored);
    }
}
