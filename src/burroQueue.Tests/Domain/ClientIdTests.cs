using burroQueue.Domain.Model;
using FluentAssertions;
using Xunit;

namespace burroQueue.Tests.Domain;

public sealed class ClientIdTests
{
    [Fact]
    public void New_ShouldGenerateUniqueClientIds()
    {
        ClientId id1 = ClientId.New();
        ClientId id2 = ClientId.New();

        id1.Should().NotBe(id2);
        id1.Value.Should().NotBeEmpty();
    }

    [Fact]
    public void ToString_ShouldFormatAsGuidString()
    {
        Guid guid = Guid.NewGuid();
        ClientId id = new(guid);

        id.ToString().Should().Be(guid.ToString("D"));
    }
}
