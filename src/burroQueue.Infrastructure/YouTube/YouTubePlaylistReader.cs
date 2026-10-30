using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using burroQueue.Domain.Model;
using burroQueue.Infrastructure.Playback;

namespace burroQueue.Infrastructure.YouTube;

public sealed class YouTubePlaylistReader : IYouTubePlaylistReader
{
    private const int MaxContinuationPages = 5;

    private static readonly string[] _invidiousInstances =
    [
        "https://invidious.jing.rocks",
        "https://inv.tux.pizza",
        "https://invidious.nerdvpn.de"
    ];

    private readonly HttpClient _httpClient;
    private readonly bool _hasExplicitHttpClient;
    private readonly string? _ytDlpPath;

    internal static Func<string, string, CancellationToken, Task<string?>> ProcessRunner { get; set; } = DefaultRunProcessAsync;

    public YouTubePlaylistReader(HttpClient? httpClient = null, string? ytDlpPath = null)
    {
        _hasExplicitHttpClient = httpClient is not null;
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(6) };
        _ytDlpPath = ytDlpPath ?? YouTubeAudioStreamResolver.FindYtDlp();
    }

    public async Task<IReadOnlyList<Track>> ReadPlaylistTracksAsync(string playlistUrlOrId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(playlistUrlOrId))
        {
            return [];
        }

        string rawInput = playlistUrlOrId.Trim();
        string trimmedId = ExtractPlaylistId(rawInput);

        // 1. Primary: YouTube InnerTube browse API (preserves collaborator names & user profile avatars!)
        bool shouldUseInnerTube = _hasExplicitHttpClient || ProcessRunner == DefaultRunProcessAsync;
        if (shouldUseInnerTube && !string.IsNullOrWhiteSpace(trimmedId))
        {
            bool preferMusicClient = rawInput.Contains("music.youtube.com", StringComparison.OrdinalIgnoreCase);
            IReadOnlyList<Track> innerTubeTracks = await TryReadViaInnerTubeAsync(trimmedId, preferMusicClient, cancellationToken).ConfigureAwait(false);
            if (innerTubeTracks.Count > 0)
            {
                return innerTubeTracks;
            }
        }

        // 2. Fallback: yt-dlp flat-playlist JSON
        string effectiveYtDlp = _ytDlpPath ?? YouTubeAudioStreamResolver.FindYtDlp() ?? string.Empty;
        if (!string.IsNullOrEmpty(effectiveYtDlp))
        {
            try
            {
                string targetArg = rawInput.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                    ? rawInput
                    : $"https://www.youtube.com/playlist?list={rawInput}";

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
                // Fallback to Invidious Web-API
            }
        }

        if (string.IsNullOrWhiteSpace(trimmedId))
        {
            return [];
        }

        // 3. Fallback: Invidious public instances with Cache-Control: no-cache
        foreach (string instance in _invidiousInstances)
        {
            try
            {
                string url = $"{instance}/api/v1/playlists/{trimmedId}";
                using HttpRequestMessage request = new(HttpMethod.Get, url);
                request.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true, NoStore = true };

                using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
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
                            tracks.Add(new Track(
                                videoId,
                                videoUri,
                                displayTitle,
                                submitter,
                                DateTimeOffset.UtcNow,
                                artistName: author,
                                isSyncedFromPlaylist: true));
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
                // Try next instance on network or parse error
            }
        }

        return [];
    }

    private async Task<IReadOnlyList<Track>> TryReadViaInnerTubeAsync(
        string playlistId,
        bool preferMusicClient,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<Track> primary = await FetchInnerTubeClientPlaylistAsync(
            playlistId,
            useMusicRemix: preferMusicClient,
            cancellationToken).ConfigureAwait(false);

        if (primary.Count > 0 && (preferMusicClient || primary.Any(t => t.SubmitterAvatarUrl is not null)))
        {
            return primary;
        }

        if (!preferMusicClient)
        {
            IReadOnlyList<Track> musicAttempt = await FetchInnerTubeClientPlaylistAsync(
                playlistId,
                useMusicRemix: true,
                cancellationToken).ConfigureAwait(false);

            if (musicAttempt.Any(t => t.SubmitterAvatarUrl is not null) || primary.Count == 0)
            {
                if (musicAttempt.Count > 0)
                {
                    return musicAttempt;
                }
            }
        }

        return primary;
    }

    private async Task<IReadOnlyList<Track>> FetchInnerTubeClientPlaylistAsync(
        string playlistId,
        bool useMusicRemix,
        CancellationToken cancellationToken)
    {
        try
        {
            string browseId = playlistId.StartsWith("VL", StringComparison.Ordinal) ? playlistId : $"VL{playlistId}";
            string endpoint = useMusicRemix
                ? "https://music.youtube.com/youtubei/v1/browse?prettyPrint=false"
                : "https://www.youtube.com/youtubei/v1/browse?prettyPrint=false";
            string clientName = useMusicRemix ? "WEB_REMIX" : "WEB";
            string clientVersion = useMusicRemix ? "1.20261028.01.00" : "2.20261028.01.00";

            List<Track> collected = [];
            HashSet<string> seenIds = new(StringComparer.Ordinal);
            string? continuation = null;
            string? ownerName = null;
            Uri? ownerAvatar = null;

            for (int page = 0; page < MaxContinuationPages; page++)
            {
                string targetField = page == 0
                    ? $"\"browseId\": \"{EscapeJson(browseId)}\""
                    : $"\"continuation\": \"{EscapeJson(continuation!)}\"";

                string payload = $$"""
                {
                  "context": {
                    "client": {
                      "hl": "de",
                      "gl": "DE",
                      "clientName": "{{clientName}}",
                      "clientVersion": "{{clientVersion}}"
                    }
                  },
                  {{targetField}}
                }
                """;

                using HttpRequestMessage request = new(HttpMethod.Post, endpoint)
                {
                    Content = new StringContent(payload, Encoding.UTF8, "application/json")
                };
                request.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true, NoStore = true };

                using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    break;
                }

                string json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                InnerTubePlaylistParseResult parsed = InnerTubePlaylistParser.ParseBrowseResponse(json, ownerName, ownerAvatar);
                ownerName ??= parsed.PlaylistOwnerName;
                ownerAvatar ??= parsed.PlaylistOwnerAvatarUrl;

                foreach (Track track in parsed.Tracks)
                {
                    if (seenIds.Add(track.VideoId))
                    {
                        collected.Add(track);
                    }
                }

                if (string.IsNullOrWhiteSpace(parsed.ContinuationToken) ||
                    string.Equals(parsed.ContinuationToken, continuation, StringComparison.Ordinal))
                {
                    break;
                }

                continuation = parsed.ContinuationToken;
            }

            return collected;
        }
        catch
        {
            return [];
        }
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
                        tracks.Add(new Track(
                            videoId,
                            videoUri,
                            displayTitle,
                            submitter,
                            DateTimeOffset.UtcNow,
                            artistName: author,
                            isSyncedFromPlaylist: true));
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

    private static string EscapeJson(string input)
    {
        return input.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
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
