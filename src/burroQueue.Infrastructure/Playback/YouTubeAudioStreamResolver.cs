using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;

namespace burroQueue.Infrastructure.Playback;

public sealed class YouTubeAudioStreamResolver : IStreamUriResolver
{
    private readonly HttpClient _httpClient;
    private readonly bool _explicitlyDisabledYtDlp;
    private string? _ytDlpPath;

    public bool IsYtDlpAvailable => !string.IsNullOrEmpty(_ytDlpPath);

    public async Task<string?> GetYtDlpVersionAsync(CancellationToken cancellationToken = default)
    {
        if (_explicitlyDisabledYtDlp || string.IsNullOrWhiteSpace(_ytDlpPath))
        {
            return null;
        }

        try
        {
            string? output = await ProcessRunner(_ytDlpPath, "--version", cancellationToken).ConfigureAwait(false);
            return string.IsNullOrWhiteSpace(output) ? null : output.Trim();
        }
        catch
        {
            return null;
        }
    }

    internal static Func<string, string, CancellationToken, Task<string?>> ProcessRunner { get; set; } = DefaultRunProcessAsync;

    public YouTubeAudioStreamResolver(HttpClient? httpClient = null, string? ytDlpPath = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        _explicitlyDisabledYtDlp = ytDlpPath is not null && ytDlpPath.Length == 0;
        _ytDlpPath = ytDlpPath ?? FindYtDlp();
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    internal static string? FindYtDlp()
    {
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string burroBinYtDlp = Path.Combine(localAppData, "burroQueue", "bin", "yt-dlp.exe");
        if (File.Exists(burroBinYtDlp))
        {
            return burroBinYtDlp;
        }

        string appLocalYtDlp = Path.Combine(AppContext.BaseDirectory, "yt-dlp.exe");
        if (File.Exists(appLocalYtDlp))
        {
            return appLocalYtDlp;
        }

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

    public async Task<string?> EnsureYtDlpAsync(CancellationToken cancellationToken = default)
    {
        string? existing = FindYtDlp();
        if (!string.IsNullOrEmpty(existing))
        {
            _ytDlpPath = existing;
            return existing;
        }

        try
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string binDir = Path.Combine(localAppData, "burroQueue", "bin");
            Directory.CreateDirectory(binDir);
            string targetPath = Path.Combine(binDir, "yt-dlp.exe");

            using HttpResponseMessage response = await _httpClient.GetAsync("https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe", cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                byte[] data = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
                await File.WriteAllBytesAsync(targetPath, data, cancellationToken).ConfigureAwait(false);
                _ytDlpPath = targetPath;
                return targetPath;
            }
        }
        catch
        {
            // Fail-safe
        }

        return null;
    }

    public async Task<Uri?> ResolveAudioStreamUriAsync(string videoId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(videoId))
        {
            return null;
        }

        string cleanId = videoId.Trim();

        if (!_explicitlyDisabledYtDlp && string.IsNullOrEmpty(_ytDlpPath))
        {
            _ytDlpPath = FindYtDlp() ?? await EnsureYtDlpAsync(cancellationToken).ConfigureAwait(false);
        }

        if (!string.IsNullOrEmpty(_ytDlpPath))
        {
            try
            {
                string cacheDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "burroQueue", "cache");
                Directory.CreateDirectory(cacheDir);
                string cachedFilePath = Path.Combine(cacheDir, $"{cleanId}.mp4");

                if (ProcessRunner == DefaultRunProcessAsync && File.Exists(cachedFilePath) && new FileInfo(cachedFilePath).Length > 0)
                {
                    return new Uri(cachedFilePath);
                }

                string args = $"--extractor-args \"youtube:player_client=android\" -f \"18/b\" --no-playlist -o \"{cachedFilePath}\" \"https://www.youtube.com/watch?v={cleanId}\"";
                string? output = await ProcessRunner(_ytDlpPath, args, cancellationToken).ConfigureAwait(false);

                if (File.Exists(cachedFilePath) && new FileInfo(cachedFilePath).Length > 0)
                {
                    PruneCacheDirectory(cacheDir, maxFiles: 15);
                    return new Uri(cachedFilePath);
                }

                if (!string.IsNullOrWhiteSpace(output))
                {
                    string firstUrl = output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)[0].Trim();
                    if (Uri.TryCreate(firstUrl, UriKind.Absolute, out Uri? audioUri))
                    {
                        return audioUri;
                    }
                }
            }
            catch
            {
                // Fallback zu API
            }
        }

        try
        {
            using HttpResponseMessage res = await _httpClient.GetAsync($"https://inv.tux.pizza/api/v1/videos/{cleanId}", cancellationToken).ConfigureAwait(false);
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

        return new Uri($"https://www.youtube.com/watch?v={cleanId}");
    }

    public void RemoveCachedAudioFile(string videoId)
    {
        if (string.IsNullOrWhiteSpace(videoId))
        {
            return;
        }

        try
        {
            string cleanId = Path.GetFileName(videoId.Trim());
            string cacheFile = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "burroQueue",
                "cache",
                $"{cleanId}.mp4");

            if (File.Exists(cacheFile))
            {
                File.Delete(cacheFile);
            }
        }
        catch
        {
            // Ignore locked or already deleted cache files
        }
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private static void PruneCacheDirectory(string cacheDir, int maxFiles)
    {
        try
        {
            FileInfo[] files = new DirectoryInfo(cacheDir).GetFiles("*.mp4");
            if (files.Length <= maxFiles)
            {
                return;
            }

            foreach (FileInfo oldFile in files.OrderByDescending(f => f.LastWriteTimeUtc).Skip(maxFiles))
            {
                try
                {
                    oldFile.Delete();
                }
                catch
                {
                }
            }
        }
        catch
        {
        }
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
