using burroQueue.Domain.Model;

namespace burroQueue.Infrastructure.Persistence;

public sealed record StoredTrackDto(
    string VideoId,
    string Url,
    string Title,
    string SubmittedBy,
    DateTimeOffset EnqueuedAtUtc,
    bool IsCurrent,
    string? ArtistName = null,
    string? SubmitterAvatarUrl = null,
    bool IsSyncedFromPlaylist = false)
{
    public static StoredTrackDto FromDomain(Track track, bool isCurrent) =>
        new(
            track.VideoId,
            track.Url.ToString(),
            track.Title,
            track.SubmittedBy,
            track.EnqueuedAtUtc,
            isCurrent,
            track.ArtistName,
            track.SubmitterAvatarUrl?.ToString(),
            track.IsSyncedFromPlaylist);

    public Track ToDomain()
    {
        Uri? avatarUri = null;
        if (!string.IsNullOrWhiteSpace(SubmitterAvatarUrl))
        {
            _ = Uri.TryCreate(SubmitterAvatarUrl, UriKind.Absolute, out avatarUri);
        }

        return new Track(
            VideoId,
            new Uri(Url),
            Title,
            SubmittedBy,
            EnqueuedAtUtc,
            artistName: ArtistName,
            submitterAvatarUrl: avatarUri,
            isSyncedFromPlaylist: IsSyncedFromPlaylist);
    }
}
