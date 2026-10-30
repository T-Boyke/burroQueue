namespace burroQueue.Infrastructure.Network.Messages;

public sealed record TrackUpvoteMessage(
    Guid GroupId,
    string VideoId,
    string TrackUrl,
    string Title,
    string VoterUsername,
    bool IsUpvoted,
    DateTimeOffset Timestamp);
