using burroQueue.Domain.Model;

namespace burroQueue.Infrastructure.YouTube;

public interface IYouTubePlaylistReader
{
    Task<IReadOnlyList<Track>> ReadPlaylistTracksAsync(string playlistUrlOrId, CancellationToken cancellationToken = default);
}
