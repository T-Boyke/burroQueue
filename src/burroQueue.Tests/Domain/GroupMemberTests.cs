using burroQueue.Domain.Model;
using FluentAssertions;
using Xunit;

namespace burroQueue.Tests.Domain;

public sealed class GroupMemberTests
{
    [Fact]
    public void Constructor_WhenValidArguments_ShouldSetProperties()
    {
        ClientId id = ClientId.New();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        GroupMember member = new(id, "Alice", "192.168.1.50", now);

        member.Id.Should().Be(id);
        member.DisplayName.Should().Be("Alice");
        member.EndpointAddress.Should().Be("192.168.1.50");
        member.JoinedAtUtc.Should().Be(now);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Constructor_WhenEmptyDisplayName_ShouldUseDefault(string? emptyName)
    {
        ClientId id = ClientId.New();
        GroupMember member = new(id, emptyName!, "192.168.1.50", DateTimeOffset.UtcNow);

        member.DisplayName.Should().Be("Anonymous Burro");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Constructor_WhenEmptyEndpoint_ShouldDefaultToLocalhost(string? emptyEndpoint)
    {
        ClientId id = ClientId.New();
        GroupMember member = new(id, "Bob", emptyEndpoint!, DateTimeOffset.UtcNow);

        member.EndpointAddress.Should().Be("127.0.0.1");
    }
}
