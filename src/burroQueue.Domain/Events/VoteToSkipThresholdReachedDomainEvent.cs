using burroQueue.Domain.Model;

namespace burroQueue.Domain.Events;

public sealed record VoteToSkipThresholdReachedDomainEvent(TrackId TrackId, int TotalVotes, DateTimeOffset OccurredAtUtc);
