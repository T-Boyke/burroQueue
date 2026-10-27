namespace burroQueue.Infrastructure.Network.Messages;

public sealed record DiscoveryBeaconMessage(
    Guid GroupId,
    string GroupName,
    Guid HostClientId,
    string HostDisplayName,
    int MemberCount,
    bool HasCollaborativePlaylist,
    string? CollaborativePlaylistUrl,
    DateTimeOffset TimestampUtc);

