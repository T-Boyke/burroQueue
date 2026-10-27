using System.Net.Http;
using System.Text.Json;
using burroQueue.Domain.Model;

namespace burroQueue.Infrastructure.YouTube;

public sealed class YouTubeMetadataFetcher : IYouTubeMetadataFetcher
{
    private readonly HttpClient _httpClient;

    public YouTubeMetadataFetcher(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
    }

    public async Task<TrackMetadata> FetchMetadataAsync(string videoId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(videoId))
        {
            return TrackMetadata.Unknown(videoId);
        }

        string oembedUrl = $"https://www.youtube.com/oembed?url=https://www.youtube.com/watch?v={Uri.EscapeDataString(videoId)}&format=json";

        try
        {
            using HttpResponseMessage response = await _httpClient.GetAsync(oembedUrl, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return TrackMetadata.Unknown(videoId);
            }

            await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using JsonDocument doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);

            string title = doc.RootElement.TryGetProperty("title", out JsonElement titleElem)
                ? titleElem.GetString() ?? videoId
                : videoId;

            string author = doc.RootElement.TryGetProperty("author_name", out JsonElement authorElem)
                ? authorElem.GetString() ?? "YouTube Artist"
                : "YouTube Artist";

            string? thumbUrl = doc.RootElement.TryGetProperty("thumbnail_url", out JsonElement thumbElem)
                ? thumbElem.GetString()
                : null;

            Uri? parsedThumbUri = null;
            if (!string.IsNullOrEmpty(thumbUrl))
            {
                _ = Uri.TryCreate(thumbUrl, UriKind.Absolute, out parsedThumbUri);
            }

            return new TrackMetadata(title, author, parsedThumbUri);
        }
        catch
        {
            return TrackMetadata.Unknown(videoId);
        }
    }
}

