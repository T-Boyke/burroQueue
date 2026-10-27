using burroQueue.Domain.Model;

namespace burroQueue.Domain.Events;

public sealed record TrackRemovedDomainEvent(TrackId TrackId, DateTimeOffset OccurredAtUtc);
