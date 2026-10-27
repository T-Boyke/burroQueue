namespace burroQueue.Infrastructure.Network.Messages;

public sealed record GroupActionMessage(
    Guid GroupId,
    Guid ActorClientId,
    string ActionType, // "Rename", "Join", "Leave"
    string? Payload,
    DateTimeOffset TimestampUtc);
