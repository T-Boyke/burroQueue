namespace burroQueue.Infrastructure.Playback;

public interface IStreamUriResolver
{
    bool IsYtDlpAvailable { get; }

    Task<Uri?> ResolveAudioStreamUriAsync(string videoId, CancellationToken cancellationToken = default);

    Task<string?> GetYtDlpVersionAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<string?>(null);

    void RemoveCachedAudioFile(string videoId)
    {
    }
}
