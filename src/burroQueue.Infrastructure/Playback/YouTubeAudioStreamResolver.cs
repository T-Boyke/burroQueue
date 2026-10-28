using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;

namespace burroQueue.Infrastructure.Playback;

public sealed class YouTubeAudioStreamResolver : IStreamUriResolver
{
    private readonly HttpClient _httpClient;
    private readonly string? _ytDlpPath;

    internal static Func<string, string, CancellationToken, Task<string?>> ProcessRunner { get; set; } = DefaultRunProcessAsync;

    public YouTubeAudioStreamResolver(HttpClient? httpClient = null, string? ytDlpPath = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        _ytDlpPath = ytDlpPath ?? FindYtDlp();
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private static string? FindYtDlp()
    {
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string wingetPath = Path.Combine(localAppData, "Microsoft", "WinGet", "Links", "yt-dlp.exe");
        if (File.Exists(wingetPath))
        {
            return wingetPath;
        }

        string? pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (pathEnv is not null)
        {
            foreach (string part in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                string candidate = Path.Combine(part.Trim(), "yt-dlp.exe");
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    public async Task<Uri?> ResolveAudioStreamUriAsync(string videoId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(videoId))
        {
            return null;
        }

        if (!string.IsNullOrEmpty(_ytDlpPath))
        {
            try
            {
                string? url = await ProcessRunner(_ytDlpPath, $"-g -f ba \"https://www.youtube.com/watch?v={videoId.Trim()}\"", cancellationToken).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(url) && Uri.TryCreate(url.Trim(), UriKind.Absolute, out Uri? audioUri))
                {
                    return audioUri;
                }
            }
            catch
            {
                // Fallback zu API
            }
        }

        try
        {
            using HttpResponseMessage res = await _httpClient.GetAsync($"https://inv.tux.pizza/api/v1/videos/{videoId.Trim()}", cancellationToken).ConfigureAwait(false);
            if (res.IsSuccessStatusCode)
            {
                string json = await res.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                using JsonDocument doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("adaptiveFormats", out JsonElement formats) && formats.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement fmt in formats.EnumerateArray())
                    {
                        if (fmt.TryGetProperty("type", out JsonElement typeEl) &&
                            typeEl.GetString()?.StartsWith("audio/", StringComparison.OrdinalIgnoreCase) == true &&
                            fmt.TryGetProperty("url", out JsonElement urlEl) &&
                            !string.IsNullOrWhiteSpace(urlEl.GetString()))
                        {
                            return new Uri(urlEl.GetString()!);
                        }
                    }
                }
            }
        }
        catch
        {
            // Fallback auf Standard YouTube-URL
        }

        return new Uri($"https://www.youtube.com/watch?v={videoId.Trim()}");
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

        string? line = await process.StandardOutput.ReadLineAsync(cancellationToken).ConfigureAwait(false);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        return line;
    }
}

