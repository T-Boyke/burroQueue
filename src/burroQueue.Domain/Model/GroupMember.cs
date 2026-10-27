namespace burroQueue.Domain.Model;

public sealed class GroupMember
{
    public ClientId Id { get; }

    public string DisplayName { get; }

    public string EndpointAddress { get; }

    public DateTimeOffset JoinedAtUtc { get; }

    public GroupMember(ClientId id, string displayName, string endpointAddress, DateTimeOffset joinedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = "Anonymous Burro";
        }

        Id = id;
        DisplayName = displayName.Trim();
        EndpointAddress = string.IsNullOrWhiteSpace(endpointAddress) ? "127.0.0.1" : endpointAddress.Trim();
        JoinedAtUtc = joinedAtUtc;
    }
}
