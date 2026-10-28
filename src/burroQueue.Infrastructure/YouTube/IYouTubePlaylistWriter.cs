namespace burroQueue.Infrastructure.YouTube;

public interface IYouTubePlaylistWriter
{
    Task<bool> AddTrackToPlaylistAsync(string playlistUrl, string videoId, CancellationToken cancellationToken = default);
}

