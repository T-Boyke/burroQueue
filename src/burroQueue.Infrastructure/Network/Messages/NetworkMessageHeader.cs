namespace burroQueue.Infrastructure.Network.Messages;

public sealed record NetworkMessageHeader(
    string MessageType,
    Guid SenderId,
    Guid GroupId,
    DateTimeOffset TimestampUtc);

