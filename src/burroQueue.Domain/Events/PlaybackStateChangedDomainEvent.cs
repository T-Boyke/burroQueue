using burroQueue.Domain.Model;

namespace burroQueue.Domain.Events;

public sealed record PlaybackStateChangedDomainEvent(
    GroupId GroupId,
    PlaybackState PreviousState,
    PlaybackState NewState,
    Track? CurrentTrack,
    DateTimeOffset OccurredAtUtc);
