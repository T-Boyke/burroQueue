using burroQueue.Domain.Model;

namespace burroQueue.Domain.Events;

public sealed record CollaborativePlaylistLinkUpdatedDomainEvent(
    GroupId GroupId,
    CollaborativePlaylistLink? NewLink,
    DateTimeOffset OccurredAtUtc);

