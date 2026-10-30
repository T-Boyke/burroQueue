namespace burroQueue.Domain.Model;

public readonly record struct TrackUpvote(
    string VideoId,
    string Username,
    DateTimeOffset Timestamp);
