namespace burroQueue.Infrastructure.Playback;

public interface IStreamUriResolver
{
    bool IsYtDlpAvailable { get; }

    Task<Uri?> ResolveAudioStreamUriAsync(string videoId, CancellationToken cancellationToken = default);

    void RemoveCachedAudioFile(string videoId)
    {
    }
}
