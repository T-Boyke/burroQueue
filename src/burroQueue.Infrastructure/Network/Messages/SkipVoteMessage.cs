namespace burroQueue.Infrastructure.Network.Messages;

public sealed record SkipVoteMessage(
    Guid GroupId,
    string TrackId,
    Guid VoterClientId,
    DateTimeOffset Timestamp);
