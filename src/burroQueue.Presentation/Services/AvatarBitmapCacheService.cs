using System.IO;
using System.Net.Http;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using burroQueue.Domain.Model;

namespace burroQueue.Presentation.Services;

public sealed class AvatarBitmapCacheService : IAvatarBitmapCacheService
{
    private const int DecodeWidthPx = 48;

    public static AvatarBitmapCacheService Shared { get; } = new();

    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;
    private readonly int _maxEntries;
    private readonly object _sync = new();
    private readonly Dictionary<Uri, ImageSource> _cache = [];
    private readonly LinkedList<Uri> _lruOrder = new();
    private readonly Dictionary<Uri, Task<ImageSource?>> _inflight = [];

    public int CachedCount
    {
        get
        {
            lock (_sync)
            {
                return _cache.Count;
            }
        }
    }

    public AvatarBitmapCacheService(HttpClient? httpClient = null, int maxEntries = 32)
    {
        _ownsHttpClient = httpClient is null;
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        _maxEntries = Math.Max(1, maxEntries);
    }

    public ImageSource? TryGetCached(Uri? avatarUri)
    {
        Uri? clean = Track.SanitizeAvatarUri(avatarUri);
        if (clean is null)
        {
            return null;
        }

        lock (_sync)
        {
            if (_cache.TryGetValue(clean, out ImageSource? existing))
            {
                PromoteLruUnsafe(clean);
                return existing;
            }
        }

        return null;
    }

    public Task<ImageSource?> GetOrLoadAsync(Uri? avatarUri, CancellationToken cancellationToken = default)
    {
        Uri? clean = Track.SanitizeAvatarUri(avatarUri);
        if (clean is null)
        {
            return Task.FromResult<ImageSource?>(null);
        }

        lock (_sync)
        {
            if (_cache.TryGetValue(clean, out ImageSource? existing))
            {
                PromoteLruUnsafe(clean);
                return Task.FromResult<ImageSource?>(existing);
            }

            if (_inflight.TryGetValue(clean, out Task<ImageSource?>? pending))
            {
                return pending;
            }

            Task<ImageSource?> loadTask = LoadAndFreezeAsync(clean, cancellationToken);
            _inflight[clean] = loadTask;
            return loadTask;
        }
    }

    private async Task<ImageSource?> LoadAndFreezeAsync(Uri cleanUri, CancellationToken cancellationToken)
    {
        try
        {
            using HttpResponseMessage response = await _httpClient.GetAsync(cleanUri, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            byte[] bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            if (bytes.Length == 0 || bytes.Length > 512 * 1024)
            {
                return null;
            }

            using MemoryStream ms = new(bytes, writable: false);
            BitmapImage bmp = new();
            bmp.BeginInit();
            bmp.StreamSource = ms;
            bmp.DecodePixelWidth = DecodeWidthPx;
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            bmp.EndInit();
            bmp.Freeze();

            lock (_sync)
            {
                StoreUnsafe(cleanUri, bmp);
            }

            return bmp;
        }
        catch
        {
            return null;
        }
        finally
        {
            lock (_sync)
            {
                _inflight.Remove(cleanUri);
            }
        }
    }

    internal ImageSource? GetOrCreateUriBitmap(Uri? avatarUri)
    {
        Uri? clean = Track.SanitizeAvatarUri(avatarUri);
        if (clean is null)
        {
            return null;
        }

        lock (_sync)
        {
            if (_cache.TryGetValue(clean, out ImageSource? cached))
            {
                PromoteLruUnsafe(clean);
                return cached;
            }
        }

        try
        {
            BitmapImage bmp = new();
            bmp.BeginInit();
            bmp.UriSource = clean;
            bmp.DecodePixelWidth = DecodeWidthPx;
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            bmp.EndInit();

            lock (_sync)
            {
                StoreUnsafe(clean, bmp);
            }

            return bmp;
        }
        catch
        {
            return null;
        }
    }

    private void StoreUnsafe(Uri key, ImageSource value)
    {
        if (_cache.ContainsKey(key))
        {
            _cache[key] = value;
            PromoteLruUnsafe(key);
            return;
        }

        while (_cache.Count >= _maxEntries && _lruOrder.First is { } oldest)
        {
            _cache.Remove(oldest.Value);
            _lruOrder.RemoveFirst();
        }

        _cache[key] = value;
        _lruOrder.AddLast(key);
    }

    private void PromoteLruUnsafe(Uri key)
    {
        if (_lruOrder.Remove(key))
        {
            _lruOrder.AddLast(key);
        }
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }
}
