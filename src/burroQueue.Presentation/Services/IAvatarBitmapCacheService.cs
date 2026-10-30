using System.Windows.Media;

namespace burroQueue.Presentation.Services;

public interface IAvatarBitmapCacheService : IDisposable
{
    int CachedCount { get; }

    ImageSource? TryGetCached(Uri? avatarUri);

    Task<ImageSource?> GetOrLoadAsync(Uri? avatarUri, CancellationToken cancellationToken = default);
}
