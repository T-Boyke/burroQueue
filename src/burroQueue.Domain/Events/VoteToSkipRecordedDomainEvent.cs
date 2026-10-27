using burroQueue.Domain.Model;

namespace burroQueue.Domain.Events;

public sealed record VoteToSkipRecordedDomainEvent(TrackId TrackId, ClientId VoterId, int CurrentVotes, int RequiredVotes, DateTimeOffset OccurredAtUtc);
