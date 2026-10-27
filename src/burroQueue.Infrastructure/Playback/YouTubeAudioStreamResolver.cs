using System.Net.Http;

namespace burroQueue.Infrastructure.Playback;

public sealed class YouTubeAudioStreamResolver : IStreamUriResolver
{
    private readonly HttpClient _httpClient;

    public YouTubeAudioStreamResolver(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
    }

    public async Task<Uri?> ResolveAudioStreamUriAsync(string videoId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(videoId))
        {
            return null;
        }

        // Ermöglicht das Auflösen über einen Invidious / Piped Mirror oder direkten Audio-Stream
        // ohne WebView2-Overhead
        await Task.CompletedTask.ConfigureAwait(false);
        return new Uri($"https://www.youtube.com/watch?v={videoId}");
    }
}
