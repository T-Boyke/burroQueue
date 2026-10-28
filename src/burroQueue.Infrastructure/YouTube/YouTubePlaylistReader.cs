using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using burroQueue.Domain.Model;
using burroQueue.Infrastructure.Playback;

namespace burroQueue.Infrastructure.YouTube;

public sealed class YouTubePlaylistReader : IYouTubePlaylistReader
{
    private static readonly string[] _invidiousInstances =
    [
        "https://invidious.jing.rocks",
        "https://inv.tux.pizza",
        "https://invidious.nerdvpn.de"
    ];

    private readonly HttpClient _httpClient;
    private readonly string? _ytDlpPath;

    internal static Func<string, string, CancellationToken, Task<string?>> ProcessRunner { get; set; } = DefaultRunProcessAsync;

    public YouTubePlaylistReader(HttpClient? httpClient = null, string? ytDlpPath = null)
    {
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        _ytDlpPath = ytDlpPath ?? YouTubeAudioStreamResolver.FindYtDlp();
    }

    public async Task<IReadOnlyList<Track>> ReadPlaylistTracksAsync(string playlistUrlOrId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(playlistUrlOrId))
        {
            return [];
        }

        string effectiveYtDlp = _ytDlpPath ?? YouTubeAudioStreamResolver.FindYtDlp() ?? string.Empty;
        if (!string.IsNullOrEmpty(effectiveYtDlp))
        {
            try
            {
                string targetArg = playlistUrlOrId.Trim().StartsWith("http", StringComparison.OrdinalIgnoreCase)
                    ? playlistUrlOrId.Trim()
                    : $"https://www.youtube.com/playlist?list={playlistUrlOrId.Trim()}";

                string? jsonOutput = await ProcessRunner(effectiveYtDlp, $"--flat-playlist --dump-single-json \"{targetArg}\"", cancellationToken).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(jsonOutput))
                {
                    List<Track> ytDlpTracks = ParseYtDlpPlaylistJson(jsonOutput);
                    if (ytDlpTracks.Count > 0)
                    {
                        return ytDlpTracks;
                    }
                }
            }
            catch
            {
                // Fallback zu Web-API
            }
        }

        string trimmedId = ExtractPlaylistId(playlistUrlOrId.Trim());
        if (string.IsNullOrWhiteSpace(trimmedId))
        {
            return [];
        }

        foreach (string instance in _invidiousInstances)
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

    private static List<Track> ParseYtDlpPlaylistJson(string json)
    {
        List<Track> tracks = [];
        try
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("entries", out JsonElement entries) && entries.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement entry in entries.EnumerateArray())
                {
                    string? videoId = entry.TryGetProperty("id", out JsonElement idEl) ? idEl.GetString() : null;
                    string? title = entry.TryGetProperty("title", out JsonElement titleEl) ? titleEl.GetString() : null;
                    string? author = entry.TryGetProperty("channel", out JsonElement chEl) ? chEl.GetString() : null;
                    if (string.IsNullOrWhiteSpace(author) && entry.TryGetProperty("uploader", out JsonElement upEl))
                    {
                        author = upEl.GetString();
                    }

                    if (!string.IsNullOrWhiteSpace(videoId))
                    {
                        string displayTitle = !string.IsNullOrWhiteSpace(title) ? title : videoId;
                        string submitter = !string.IsNullOrWhiteSpace(author) ? author : "YouTube Playlist";
                        Uri videoUri = new($"https://www.youtube.com/watch?v={videoId}");
                        tracks.Add(new Track(videoId, videoUri, displayTitle, submitter, DateTimeOffset.UtcNow));
                    }
                }
            }
        }
        catch
        {
            // Fail-safe
        }

        return tracks;
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

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private static async Task<string?> DefaultRunProcessAsync(string fileName, string arguments, CancellationToken cancellationToken)
    {
        ProcessStartInfo psi = new()
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using Process process = new() { StartInfo = psi };
        process.Start();

        Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        return await stdoutTask.ConfigureAwait(false);
    }
}
