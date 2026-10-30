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

    [Fact]
    public void Constructor_WhenCollaboratorAndTrustedHttpsAvatarProvided_ShouldStoreArtistSubmitterAndAvatar()
    {
        Uri url = new("https://www.youtube.com/watch?v=eVTXPUF4Oz4");
        Uri avatar = new("https://yt3.ggpht.com/ytc/AIdro_kcollaborator=s88-c-k-c0x00ffffff-no-rj");

        Track track = new(
            "eVTXPUF4Oz4",
            url,
            "In The End",
            "Tobias",
            DateTimeOffset.UtcNow,
            artistName: "Linkin Park",
            submitterAvatarUrl: avatar,
            isSyncedFromPlaylist: true);

        track.SubmittedBy.Should().Be("Tobias");
        track.ArtistName.Should().Be("Linkin Park");
        track.SubmitterAvatarUrl.Should().Be(avatar);
        track.IsSyncedFromPlaylist.Should().BeTrue();
    }

    [Theory]
    [InlineData("http://yt3.ggpht.com/insecure.jpg")]
    [InlineData("https://evil-tracker.example.com/pixel.png")]
    public void Constructor_WhenAvatarUriIsNonHttpsOrUntrustedHost_ShouldRejectAvatarToNull(string untrustedAvatar)
    {
        Uri url = new("https://www.youtube.com/watch?v=eVTXPUF4Oz4");
        Uri badAvatar = new(untrustedAvatar);

        Track track = new(
            "eVTXPUF4Oz4",
            url,
            "In The End",
            "Tobias",
            DateTimeOffset.UtcNow,
            submitterAvatarUrl: badAvatar);

        track.SubmitterAvatarUrl.Should().BeNull();
    }
}

