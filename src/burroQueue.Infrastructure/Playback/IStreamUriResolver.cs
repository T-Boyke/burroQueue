namespace burroQueue.Infrastructure.Playback;

public interface IStreamUriResolver
{
    Task<Uri?> ResolveAudioStreamUriAsync(string videoId, CancellationToken cancellationToken = default);
}
