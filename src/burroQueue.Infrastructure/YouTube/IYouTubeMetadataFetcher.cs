using burroQueue.Domain.Model;

namespace burroQueue.Infrastructure.YouTube;

public interface IYouTubeMetadataFetcher
{
    Task<TrackMetadata> FetchMetadataAsync(string videoId, CancellationToken cancellationToken = default);
}
