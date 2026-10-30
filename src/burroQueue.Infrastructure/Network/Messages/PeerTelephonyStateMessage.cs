namespace burroQueue.Infrastructure.Network.Messages;

public sealed record PeerTelephonyStateMessage(
    Guid GroupId,
    Guid ClientId,
    string ClientDisplayName,
    string ProviderId,
    bool IsCallActive,
    DateTimeOffset Timestamp);
