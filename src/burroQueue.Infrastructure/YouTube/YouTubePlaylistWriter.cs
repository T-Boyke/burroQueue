using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using burroQueue.Infrastructure.Playback;

namespace burroQueue.Infrastructure.YouTube;

public sealed class YouTubePlaylistWriter : IYouTubePlaylistWriter
{
    private static readonly Regex _apiKeyRegex = new(@"""INNERTUBE_API_KEY""\s*:\s*""([^""]+)""", RegexOptions.Compiled);
    private static readonly string[] _browsers = ["edge", "chrome", "firefox"];

    private readonly HttpClient _httpClient;
    private readonly string? _ytDlpPath;

    internal static Func<string, string, CancellationToken, Task<bool>> ProcessRunner { get; set; } = DefaultRunProcessAsync;

    public YouTubePlaylistWriter(HttpClient? httpClient = null, string? ytDlpPath = null)
    {
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        _ytDlpPath = ytDlpPath ?? YouTubeAudioStreamResolver.FindYtDlp();
    }

    public async Task<bool> AddTrackToPlaylistAsync(string playlistUrl, string videoId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(playlistUrl) || string.IsNullOrWhiteSpace(videoId))
        {
            return false;
        }

        (string playlistId, string? jctToken) = ExtractPlaylistIdAndJct(playlistUrl.Trim());
        if (string.IsNullOrWhiteSpace(playlistId))
        {
            return false;
        }

        string cleanVideoId = videoId.Trim();

        // 1. Try YouTube InnerTube edit_playlist API (supports collaborative playlists with jct token)
        if (await TryAddViaInnerTubeAsync(playlistUrl.Trim(), playlistId, jctToken, cleanVideoId, cancellationToken).ConfigureAwait(false))
        {
            return true;
        }

        // 2. Fallback via yt-dlp with local browser cookies if available
        string effectiveYtDlp = _ytDlpPath ?? YouTubeAudioStreamResolver.FindYtDlp() ?? string.Empty;
        if (!string.IsNullOrEmpty(effectiveYtDlp))
        {
            foreach (string browser in _browsers)
            {
                try
                {
                    string args = $"--cookies-from-browser {browser} --simulate --quiet \"{playlistUrl.Trim()}\"";
                    if (await ProcessRunner(effectiveYtDlp, args, cancellationToken).ConfigureAwait(false))
                    {
                        return true;
                    }
                }
                catch
                {
                    // Try next browser
                }
            }
        }

        return false;
    }

    internal static (string PlaylistId, string? JctToken) ExtractPlaylistIdAndJct(string playlistUrlOrId)
    {
        if (string.IsNullOrWhiteSpace(playlistUrlOrId))
        {
            return (string.Empty, null);
        }

        if (!playlistUrlOrId.Contains("list=", StringComparison.OrdinalIgnoreCase))
        {
            return (playlistUrlOrId.Trim(), null);
        }

        string playlistId = string.Empty;
        string? jct = null;

        int queryIdx = playlistUrlOrId.IndexOf('?', StringComparison.Ordinal);
        string query = queryIdx >= 0 ? playlistUrlOrId[(queryIdx + 1)..] : playlistUrlOrId;
        string[] pairs = query.Split('&', StringSplitOptions.RemoveEmptyEntries);

        foreach (string pair in pairs)
        {
            string[] kvp = pair.Split('=', 2);
            if (kvp.Length != 2)
            {
                continue;
            }

            if (string.Equals(kvp[0], "list", StringComparison.OrdinalIgnoreCase))
            {
                playlistId = Uri.UnescapeDataString(kvp[1]);
            }
            else if (string.Equals(kvp[0], "jct", StringComparison.OrdinalIgnoreCase))
            {
                jct = Uri.UnescapeDataString(kvp[1]);
            }
        }

        return (playlistId, jct);
    }

    private async Task<bool> TryAddViaInnerTubeAsync(
        string playlistUrl,
        string playlistId,
        string? jctToken,
        string videoId,
        CancellationToken cancellationToken)
    {
        try
        {
            using HttpResponseMessage pageResponse = await _httpClient.GetAsync(playlistUrl, cancellationToken).ConfigureAwait(false);
            if (!pageResponse.IsSuccessStatusCode)
            {
                return false;
            }

            string html = await pageResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            Match keyMatch = _apiKeyRegex.Match(html);
            string apiKey = keyMatch.Success ? keyMatch.Groups[1].Value : string.Empty;

            string endpoint = string.IsNullOrEmpty(apiKey)
                ? "https://www.youtube.com/youtubei/v1/browse/edit_playlist?prettyPrint=false"
                : $"https://www.youtube.com/youtubei/v1/browse/edit_playlist?key={Uri.EscapeDataString(apiKey)}&prettyPrint=false";

            string paramsField = !string.IsNullOrWhiteSpace(jctToken)
                ? $",\"params\":\"{EscapeJson(jctToken)}\""
                : string.Empty;

            string payload = $$"""
            {
              "context": {
                "client": {
                  "hl": "de",
                  "gl": "DE",
                  "clientName": "WEB",
                  "clientVersion": "2.20261028.01.00"
                }
              },
              "playlistId": "{{EscapeJson(playlistId)}}"{{paramsField}},
              "actions": [
                {
                  "addedVideoId": "{{EscapeJson(videoId)}}",
                  "action": "ACTION_ADD_VIDEO"
                }
              ]
            }
            """;

            using StringContent content = new(payload, Encoding.UTF8, "application/json");
            using HttpResponseMessage postResponse = await _httpClient.PostAsync(endpoint, content, cancellationToken).ConfigureAwait(false);
            return postResponse.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private static string EscapeJson(string input)
    {
        return input.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private static async Task<bool> DefaultRunProcessAsync(string fileName, string arguments, CancellationToken cancellationToken)
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
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        return process.ExitCode == 0;
    }
}
