using burroQueue.Domain.Model;

namespace burroQueue.Domain.Events;

public sealed record TrackMetadataLoadedDomainEvent(TrackId TrackId, TrackMetadata Metadata, DateTimeOffset OccurredAtUtc);

