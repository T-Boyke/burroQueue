using burroQueue.Domain.Model;
using burroQueue.Infrastructure.Network.Messages;

namespace burroQueue.Infrastructure.Network;

public interface ILanGroupService : IAsyncDisposable
{
    event EventHandler<DiscoveryBeaconMessage>? GroupDiscovered;

    event EventHandler<SharedPlaylistSyncMessage>? CollaborativePlaylistReceived;

    event EventHandler<GroupActionMessage>? GroupActionReceived;

    event EventHandler<PlaybackCommandMessage>? PlaybackCommandReceived;

    event EventHandler<QueueSyncMessage>? QueueSyncReceived;

    event EventHandler<SkipVoteMessage>? SkipVoteReceived;

    event EventHandler<PeerTelephonyStateMessage>? TelephonyStateReceived;

    event EventHandler<QueueOrderSyncMessage>? QueueOrderSyncReceived;

    event EventHandler<TrackUpvoteMessage>? TrackUpvoteReceived;

    event EventHandler<HostHandoffMessage>? HostHandoffReceived;

    void SetBeaconResponder(Func<DiscoveryBeaconMessage?>? responder)
    {
    }

    Task StartAsync(CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);

    Task BroadcastBeaconAsync(BurroGroup group, ClientId hostId, string hostName, CancellationToken cancellationToken = default);

    Task SendDiscoveryProbeAsync(ClientId requesterId, string requesterDisplayName, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    Task SendTelephonyStateAsync(GroupId groupId, ClientId clientId, string clientDisplayName, string providerId, bool isCallActive, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    Task ShareCollaborativePlaylistAsync(GroupId groupId, string playlistUrl, string sharedByName, CancellationToken cancellationToken = default);

    Task SendPlaybackCommandAsync(GroupId groupId, ClientId issuerId, string command, CancellationToken cancellationToken = default);

    Task SendGroupActionAsync(GroupId groupId, ClientId actorId, string actionType, string? payload, CancellationToken cancellationToken = default);

    Task SendQueueSyncAsync(GroupId groupId, string action, Track track, CancellationToken cancellationToken = default);

    Task SendSkipVoteAsync(GroupId groupId, string trackId, ClientId voterId, CancellationToken cancellationToken = default);

    Task SendQueueOrderSyncAsync(GroupId groupId, ClientId issuerId, bool isShuffleEnabled, IReadOnlyList<string> orderedVideoIds, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    Task SendTrackUpvoteAsync(GroupId groupId, string videoId, string trackUrl, string title, string voterUsername, bool isUpvoted, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    Task SendHostHandoffAsync(GroupId groupId, ClientId previousHostId, QueuePlaybackCursor cursor, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}

