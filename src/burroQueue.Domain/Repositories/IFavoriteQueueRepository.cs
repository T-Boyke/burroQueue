using burroQueue.Domain.Model;

namespace burroQueue.Domain.Repositories;

public interface IFavoriteQueueRepository
{
    Task<IReadOnlyList<FavoriteTrackEntry>> LoadFavoritesAsync(string groupKey, CancellationToken cancellationToken = default);

    Task SaveFavoritesAsync(string groupKey, IReadOnlyList<FavoriteTrackEntry> favorites, CancellationToken cancellationToken = default);
}

