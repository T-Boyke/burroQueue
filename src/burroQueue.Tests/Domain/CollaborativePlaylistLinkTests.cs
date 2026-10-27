using burroQueue.Domain.Exceptions;
using burroQueue.Domain.Model;
using FluentAssertions;
using Xunit;

namespace burroQueue.Tests.Domain;

public sealed class CollaborativePlaylistLinkTests
{
    [Fact]
    public void Constructor_WhenValidCollaborativeUrl_ShouldParsePlaylistIdAndToken()
    {
        string rawUrl = "https://www.youtube.com/playlist?list=PLTest12345&jct=my_secret_token";

        CollaborativePlaylistLink link = new(rawUrl);

        link.PlaylistId.Should().Be("PLTest12345");
        link.CollaborationToken.Should().Be("my_secret_token");
        link.RawUri.Should().Be(new Uri(rawUrl));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-url")]
    [InlineData("https://www.youtube.com/playlist")]
    [InlineData("https://www.youtube.com/playlist?list=PLTest12345")]
    [InlineData("https://www.youtube.com/playlist?jct=my_secret_token")]
    public void Constructor_WhenMissingParametersOrInvalid_ShouldThrowInvalidTrackUrlException(string invalidUrl)
    {
        Action act = () => _ = new CollaborativePlaylistLink(invalidUrl);

        act.Should().Throw<InvalidTrackUrlException>();
    }
}

