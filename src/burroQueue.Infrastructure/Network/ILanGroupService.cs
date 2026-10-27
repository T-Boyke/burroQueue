using burroQueue.Domain.Model;
using burroQueue.Infrastructure.Network.Messages;

namespace burroQueue.Infrastructure.Network;

public interface ILanGroupService : IAsyncDisposable
{
    event EventHandler<DiscoveryBeaconMessage>? GroupDiscovered;

    event EventHandler<SharedPlaylistSyncMessage>? CollaborativePlaylistReceived;

    event EventHandler<GroupActionMessage>? GroupActionReceived;

    event EventHandler<PlaybackCommandMessage>? PlaybackCommandReceived;

    Task StartAsync(CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);

    Task BroadcastBeaconAsync(BurroGroup group, ClientId hostId, string hostName, CancellationToken cancellationToken = default);

    Task ShareCollaborativePlaylistAsync(GroupId groupId, string playlistUrl, string sharedByName, CancellationToken cancellationToken = default);

    Task SendPlaybackCommandAsync(GroupId groupId, ClientId issuerId, string command, CancellationToken cancellationToken = default);

    Task SendGroupActionAsync(GroupId groupId, ClientId actorId, string actionType, string? payload, CancellationToken cancellationToken = default);
}
