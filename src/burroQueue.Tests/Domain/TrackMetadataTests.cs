using burroQueue.Domain.Model;
using FluentAssertions;
using Xunit;

namespace burroQueue.Tests.Domain;

public sealed class TrackMetadataTests
{
    [Fact]
    public void Constructor_WhenValidArguments_ShouldSetProperties()
    {
        Uri thumb = new("https://img.youtube.com/vi/123/0.jpg");
        TrackMetadata meta = new("Song Title", "Artist Name", thumb);

        meta.Title.Should().Be("Song Title");
        meta.AuthorName.Should().Be("Artist Name");
        meta.ThumbnailUri.Should().Be(thumb);
    }

    [Theory]
    [InlineData("Fallback Video", "Fallback Video")]
    [InlineData("", "Unknown Track")]
    [InlineData("   ", "Unknown Track")]
    [InlineData(null, "Unknown Track")]
    public void Unknown_ShouldProvideFallbackMetadata(string? fallback, string expectedTitle)
    {
        TrackMetadata meta = TrackMetadata.Unknown(fallback!);

        meta.Title.Should().Be(expectedTitle);
        meta.AuthorName.Should().Be("Unknown Artist");
        meta.ThumbnailUri.Should().BeNull();
    }
}
