using burroQueue.Domain.Exceptions;
using burroQueue.Domain.Model;
using FluentAssertions;
using Xunit;

namespace burroQueue.Tests.Domain;

public sealed class TrackTests
{
    [Fact]
    public void Constructor_WhenValidArgumentsProvided_ShouldInitializeProperties()
    {
        Uri url = new("https://www.youtube.com/watch?v=dQw4w9WgXcQ");
        DateTimeOffset now = DateTimeOffset.UtcNow;

        Track track = new("dQw4w9WgXcQ", url, "Never Gonna Give You Up", "Tobias", now);

        track.Id.Value.Should().NotBeEmpty();
        track.VideoId.Should().Be("dQw4w9WgXcQ");
        track.Url.Should().Be(url);
        track.Title.Should().Be("Never Gonna Give You Up");
        track.SubmittedBy.Should().Be("Tobias");
        track.EnqueuedAtUtc.Should().Be(now);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WhenVideoIdIsInvalid_ShouldThrowInvalidTrackUrlException(string invalidVideoId)
    {
        Uri url = new("https://www.youtube.com/watch?v=dQw4w9WgXcQ");

        Action act = () => _ = new Track(invalidVideoId, url, "Title", "User", DateTimeOffset.UtcNow);

        act.Should().Throw<InvalidTrackUrlException>();
    }
}

