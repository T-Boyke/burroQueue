using burroQueue.Domain.Model;

namespace burroQueue.Infrastructure.Persistence;

public sealed record StoredTrackDto(
    string VideoId,
    string Url,
    string Title,
    string SubmittedBy,
    DateTimeOffset EnqueuedAtUtc,
    bool IsCurrent)
{
    public static StoredTrackDto FromDomain(Track track, bool isCurrent) =>
        new(track.VideoId, track.Url.ToString(), track.Title, track.SubmittedBy, track.EnqueuedAtUtc, isCurrent);

    public Track ToDomain() =>
        new(VideoId, new Uri(Url), Title, SubmittedBy, EnqueuedAtUtc);
}
