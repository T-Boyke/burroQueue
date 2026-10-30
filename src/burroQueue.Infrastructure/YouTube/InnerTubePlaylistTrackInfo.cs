namespace burroQueue.Infrastructure.YouTube;

internal readonly record struct InnerTubePlaylistTrackInfo(
    string VideoId,
    string Title,
    string? ArtistName,
    string? ContributorName,
    Uri? ContributorAvatar);
