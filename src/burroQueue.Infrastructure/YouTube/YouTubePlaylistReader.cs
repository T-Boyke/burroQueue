using System.Net.Http;
using System.Text.Json;
using burroQueue.Domain.Model;

namespace burroQueue.Infrastructure.YouTube;

public sealed class YouTubePlaylistReader : IYouTubePlaylistReader
{
    private static readonly string[] InvidiousInstances =
    [
        "https://invidious.jing.rocks",
        "https://inv.tux.pizza",
        "https://invidious.nerdvpn.de"
    ];

    private readonly HttpClient _httpClient;

    public YouTubePlaylistReader(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
    }

    public async Task<IReadOnlyList<Track>> ReadPlaylistTracksAsync(string playlistUrlOrId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(playlistUrlOrId))
        {
            return [];
        }

        string trimmedId = ExtractPlaylistId(playlistUrlOrId.Trim());
        if (string.IsNullOrWhiteSpace(trimmedId))
        {
            return [];
        }

        foreach (string instance in InvidiousInstances)
        {
            try
            {
                string url = $"{instance}/api/v1/playlists/{trimmedId}";
                using HttpResponseMessage response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    continue;
                }

                string json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                using JsonDocument doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("videos", out JsonElement videosElement) && videosElement.ValueKind == JsonValueKind.Array)
                {
                    List<Track> tracks = [];
                    foreach (JsonElement video in videosElement.EnumerateArray())
                    {
                        string? videoId = video.TryGetProperty("videoId", out JsonElement idProp) ? idProp.GetString() : null;
                        string? title = video.TryGetProperty("title", out JsonElement titleProp) ? titleProp.GetString() : null;
                        string? author = video.TryGetProperty("author", out JsonElement authorProp) ? authorProp.GetString() : null;

                        if (!string.IsNullOrWhiteSpace(videoId))
                        {
                            string displayTitle = !string.IsNullOrWhiteSpace(title) ? title : videoId;
                            string submitter = !string.IsNullOrWhiteSpace(author) ? author : "YouTube Playlist";
                            Uri videoUri = new($"https://www.youtube.com/watch?v={videoId}");
                            tracks.Add(new Track(videoId, videoUri, displayTitle, submitter, DateTimeOffset.UtcNow));
                        }
                    }

                    if (tracks.Count > 0)
                    {
                        return tracks;
                    }
                }
            }
            catch
            {
                // Versuche nächste Instanz bei Netzwerk-/Parsingfehlern
            }
        }

        return [];
    }

    private static string ExtractPlaylistId(string input)
    {
        if (input.Contains("list=", StringComparison.OrdinalIgnoreCase))
        {
            int listIdx = input.IndexOf("list=", StringComparison.OrdinalIgnoreCase) + 5;
            int endIdx = input.IndexOf('&', listIdx);
            return endIdx >= 0 ? input[listIdx..endIdx] : input[listIdx..];
        }

        return input;
    }
}
