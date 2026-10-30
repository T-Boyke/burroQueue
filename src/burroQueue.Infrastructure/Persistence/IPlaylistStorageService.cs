using burroQueue.Domain.Model;

namespace burroQueue.Infrastructure.Persistence;

public interface IPlaylistStorageService
{
    QueuePlaybackCursor LastLoadedCursor => QueuePlaybackCursor.Empty;

    void UpdatePlaybackCursor(QueuePlaybackCursor cursor)
    {
    }

    Task SavePlaylistAsync(Playlist playlist, CancellationToken cancellationToken = default);

    Task LoadPlaylistAsync(Playlist playlist, CancellationToken cancellationToken = default);

    async Task SaveSessionAsync(Playlist playlist, QueuePlaybackCursor cursor, CancellationToken cancellationToken = default)
    {
        UpdatePlaybackCursor(cursor);
        await SavePlaylistAsync(playlist, cancellationToken).ConfigureAwait(false);
    }

    async Task<QueuePlaybackCursor> LoadSessionAsync(Playlist playlist, CancellationToken cancellationToken = default)
    {
        await LoadPlaylistAsync(playlist, cancellationToken).ConfigureAwait(false);
        return LastLoadedCursor;
    }
}
