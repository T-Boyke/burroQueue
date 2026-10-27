namespace burroQueue.Infrastructure.Network.Messages;

public sealed record PlaybackCommandMessage(
    Guid GroupId,
    Guid IssuerClientId,
    string Command, // "Play", "Pause", "Stop", "Skip"
    string? OptionalTrackVideoId,
    DateTimeOffset TimestampUtc);

