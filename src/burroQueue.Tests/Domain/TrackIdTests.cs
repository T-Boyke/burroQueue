using burroQueue.Domain.Model;
using FluentAssertions;
using Xunit;

namespace burroQueue.Tests.Domain;

public sealed class TrackIdTests
{
    [Fact]
    public void New_ShouldGenerateUniqueTrackIds()
    {
        TrackId id1 = TrackId.New();
        TrackId id2 = TrackId.New();

        id1.Should().NotBe(id2);
        id1.Value.Should().NotBeEmpty();
    }

    [Fact]
    public void ToString_ShouldFormatAsGuidWithoutHyphens()
    {
        Guid guid = Guid.NewGuid();
        TrackId id = new(guid);

        id.ToString().Should().Be(guid.ToString("N"));
    }
}
