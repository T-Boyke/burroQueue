namespace burroQueue.Infrastructure.Network.Messages;

public sealed record QueueOrderSyncMessage(
    Guid GroupId,
    Guid IssuerClientId,
    bool IsShuffleEnabled,
    IReadOnlyList<string> OrderedVideoIds,
    DateTimeOffset Timestamp);

