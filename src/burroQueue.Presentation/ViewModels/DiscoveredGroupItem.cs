namespace burroQueue.Presentation.ViewModels;

public sealed record DiscoveredGroupItem(
    Guid GroupId,
    string GroupName,
    int MemberCount,
    string HostEndpoint,
    DateTimeOffset LastSeen);
