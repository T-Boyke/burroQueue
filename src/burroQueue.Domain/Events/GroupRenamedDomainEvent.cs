using burroQueue.Domain.Model;

namespace burroQueue.Domain.Events;

public sealed record GroupRenamedDomainEvent(GroupId GroupId, string OldName, string NewName, DateTimeOffset OccurredAtUtc);
