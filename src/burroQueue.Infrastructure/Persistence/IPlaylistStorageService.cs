using burroQueue.Domain.Model;

namespace burroQueue.Infrastructure.Persistence;

public interface IPlaylistStorageService
{
    Task SavePlaylistAsync(Playlist playlist, CancellationToken cancellationToken = default);

    Task LoadPlaylistAsync(Playlist playlist, CancellationToken cancellationToken = default);
}
