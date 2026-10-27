namespace burroQueue.Infrastructure.Network.Messages;

public sealed record SharedPlaylistSyncMessage(
    Guid GroupId,
    string CollaborativePlaylistUrl,
    string SharedByDisplayName,
    DateTimeOffset TimestampUtc);
