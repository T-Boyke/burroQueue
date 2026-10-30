using burroQueue.Domain.Model;

namespace burroQueue.Infrastructure.YouTube;

public sealed record InnerTubePlaylistParseResult(
    IReadOnlyList<Track> Tracks,
    string? ContinuationToken,
    string? PlaylistOwnerName,
    Uri? PlaylistOwnerAvatarUrl);

