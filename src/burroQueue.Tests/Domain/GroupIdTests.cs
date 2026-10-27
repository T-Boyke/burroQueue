using burroQueue.Domain.Model;
using FluentAssertions;
using Xunit;

namespace burroQueue.Tests.Domain;

public sealed class GroupIdTests
{
    [Fact]
    public void New_ShouldGenerateUniqueGroupIds()
    {
        GroupId id1 = GroupId.New();
        GroupId id2 = GroupId.New();

        id1.Should().NotBe(id2);
        id1.Value.Should().NotBeEmpty();
    }

    [Fact]
    public void ToString_ShouldFormatAsGuidString()
    {
        Guid guid = Guid.NewGuid();
        GroupId id = new(guid);

        id.ToString().Should().Be(guid.ToString("D"));
    }
}
