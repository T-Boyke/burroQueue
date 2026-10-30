namespace burroQueue.Domain.Model;

public sealed record PlaylistReconciliationResult(
    IReadOnlyList<Track> AddedTracks,
    IReadOnlyList<Track> RemovedTracks,
    int UpdatedAttributionCount)
{
    public bool HasChanges => AddedTracks.Count > 0 || RemovedTracks.Count > 0 || UpdatedAttributionCount > 0;
}
