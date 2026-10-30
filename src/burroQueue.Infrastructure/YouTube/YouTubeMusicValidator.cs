using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using burroQueue.Domain.Model;
using burroQueue.Domain.Services;

namespace burroQueue.Infrastructure.YouTube;

public sealed class YouTubeMusicValidator : IYouTubeMusicValidator
{
    private const string InnerTubePlayerEndpoint = "https://www.youtube.com/youtubei/v1/player?prettyPrint=false";

    private static readonly HttpClient _sharedClient = CreateHttpClient();

    private readonly Func<string, Task<string?>> _playerJsonFetcher;

    public YouTubeMusicValidator(Func<string, Task<string?>>? playerJsonFetcher = null)
    {
        _playerJsonFetcher = playerJsonFetcher ?? FetchInnerTubePlayerJsonAsync;
    }

    public async Task<MusicValidationResult> ValidateAsync(
        string videoId,
        TimeSpan maxDuration,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(videoId))
        {
            return new MusicValidationResult(false, string.Empty, string.Empty, string.Empty, TimeSpan.Zero, "Empty video ID");
        }

        string trimmedId = videoId.Trim();
        TimeSpan effectiveMax = maxDuration > TimeSpan.Zero ? maxDuration : TimeSpan.FromMinutes(12);

        try
        {
            string? rawJson = await _playerJsonFetcher(trimmedId).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(rawJson))
            {
                return new MusicValidationResult(false, trimmedId, trimmedId, string.Empty, TimeSpan.Zero, "Could not fetch InnerTube player metadata");
            }

            using JsonDocument doc = JsonDocument.Parse(rawJson);
            JsonElement root = doc.RootElement;

            string title = trimmedId;
            string author = string.Empty;
            int lengthSeconds = 0;
            string musicVideoType = string.Empty;

            if (root.TryGetProperty("videoDetails", out JsonElement details))
            {
                if (details.TryGetProperty("title", out JsonElement t) && t.ValueKind == JsonValueKind.String)
                {
                    title = t.GetString() ?? trimmedId;
                }

                if (details.TryGetProperty("author", out JsonElement a) && a.ValueKind == JsonValueKind.String)
                {
                    author = a.GetString() ?? string.Empty;
                }

                if (details.TryGetProperty("lengthSeconds", out JsonElement l))
                {
                    if (l.ValueKind == JsonValueKind.String)
                    {
                        _ = int.TryParse(l.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out lengthSeconds);
                    }
                    else if (l.ValueKind == JsonValueKind.Number)
                    {
                        lengthSeconds = l.GetInt32();
                    }
                }

                if (details.TryGetProperty("musicVideoType", out JsonElement mvt) && mvt.ValueKind == JsonValueKind.String)
                {
                    musicVideoType = mvt.GetString() ?? string.Empty;
                }
            }

            string category = string.Empty;
            if (root.TryGetProperty("microformat", out JsonElement mf) &&
                mf.TryGetProperty("playerMicroformatRenderer", out JsonElement pmr) &&
                pmr.TryGetProperty("category", out JsonElement catElem) &&
                catElem.ValueKind == JsonValueKind.String)
            {
                category = catElem.GetString() ?? string.Empty;
            }

            TimeSpan duration = TimeSpan.FromSeconds(Math.Max(0, lengthSeconds));

            // Signal 4: Duration guard (reject podcasts/livestreams/tutorials exceeding maxDuration)
            if (duration > effectiveMax)
            {
                return new MusicValidationResult(
                    false,
                    trimmedId,
                    title,
                    author,
                    duration,
                    $"Duration ({duration:mm\\:ss}) exceeds max music duration ({effectiveMax:mm\\:ss})");
            }

            // Signal 1: InnerTube musicVideoType (ATV, OMV, UGC, OFFICIAL_SOURCE_MUSIC)
            bool hasMusicVideoType = musicVideoType.StartsWith("MUSIC_VIDEO_TYPE_", StringComparison.OrdinalIgnoreCase);

            // Signal 2: Microformat Category == "Music"
            bool isMusicCategory = string.Equals(category, "Music", StringComparison.OrdinalIgnoreCase);

            // Signal 3: Official Artist / Topic channel or Music attribution
            bool hasTopicOrOfficialSignal =
                author.EndsWith(" - Topic", StringComparison.OrdinalIgnoreCase) ||
                author.Contains("VEVO", StringComparison.OrdinalIgnoreCase) ||
                title.Contains("Official Audio", StringComparison.OrdinalIgnoreCase) ||
                title.Contains("Official Music Video", StringComparison.OrdinalIgnoreCase);

            if (hasMusicVideoType || isMusicCategory || hasTopicOrOfficialSignal)
            {
                return new MusicValidationResult(true, trimmedId, title, author, duration, null);
            }

            return new MusicValidationResult(
                false,
                trimmedId,
                title,
                author,
                duration,
                $"Category '{category}' is not verified Music");
        }
        catch (Exception ex)
        {
            return new MusicValidationResult(false, trimmedId, trimmedId, string.Empty, TimeSpan.Zero, ex.Message);
        }
    }

    private static async Task<string?> FetchInnerTubePlayerJsonAsync(string videoId)
    {
        string payload = $$"""
        {
          "videoId": "{{videoId}}",
          "context": {
            "client": {
              "clientName": "WEB_REMIX",
              "clientVersion": "1.20260101.01.00",
              "hl": "en",
              "gl": "US"
            }
          }
        }
        """;

        using StringContent content = new(payload, Encoding.UTF8, "application/json");
        using HttpResponseMessage response = await _sharedClient.PostAsync(InnerTubePlayerEndpoint, content).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
    }

    private static HttpClient CreateHttpClient()
    {
        HttpClient client = new()
        {
            Timeout = TimeSpan.FromSeconds(5)
        };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("burroQueue", "1.2"));
        return client;
    }
}

