namespace burroQueue.Infrastructure.Network.Messages;

public sealed record DiscoveryProbeMessage(
    Guid RequesterClientId,
    string RequesterDisplayName,
    DateTimeOffset Timestamp);
