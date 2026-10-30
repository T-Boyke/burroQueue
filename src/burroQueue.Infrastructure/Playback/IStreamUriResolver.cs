namespace burroQueue.Infrastructure.Playback;

public interface IStreamUriResolver
{
    bool IsYtDlpAvailable { get; }

    Task<Uri?> ResolveAudioStreamUriAsync(string videoId, CancellationToken cancellationToken = default);

    Task<double> ResolveTrackLoudnessLufsAsync(string videoId, CancellationToken cancellationToken = default) =>
        Task.FromResult(-14.0);

    Task<string?> GetYtDlpVersionAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<string?>(null);

    void RemoveCachedAudioFile(string videoId)
    {
    }
}
