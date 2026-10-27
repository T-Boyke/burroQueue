namespace burroQueue.Infrastructure.Network.Messages;

public sealed record QueueSyncMessage(
    Guid GroupId,
    string Action,
    string VideoId,
    string TrackUrl,
    string Title,
    string AddedBy,
    DateTimeOffset Timestamp);
