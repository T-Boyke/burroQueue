using burroQueue.Domain.Model;
using burroQueue.Infrastructure.Web;
using FluentAssertions;
using Xunit;

namespace burroQueue.Tests.Infrastructure;

public sealed class EmbeddedWebDropServerTests
{
    [Fact]
    public void GeneratePageHtml_ContainsNowPlayingAndQueuedTracks()
    {
        // Arrange
        BurroGroup group = new(GroupId.New(), "Test Jukebox");
        Track track1 = new("vid1", new Uri("https://www.youtube.com/watch?v=vid1"), "Awesome Song 1", "UserA", DateTimeOffset.UtcNow);
        Track track2 = new("vid2", new Uri("https://www.youtube.com/watch?v=vid2"), "Chill Beats 2", "UserB", DateTimeOffset.UtcNow);
        group.Playlist.Enqueue(track1);
        group.Playlist.Enqueue(track2);

        EmbeddedWebDropServer server = new(8089, group);

        // Act
        string html = server.GeneratePageHtml();

        // Assert
        html.Should().Contain("Awesome Song 1");
        html.Should().Contain("Chill Beats 2");
        html.Should().Contain("GNU AGPL-3.0");
        html.Should().Contain("https://github.com/T-Boyke/burroQueue");
    }

    [Fact]
    public void GeneratePageHtml_WhenQueueEmpty_DisplaysEmptyState()
    {
        // Arrange
        BurroGroup group = new(GroupId.New(), "Test Jukebox");
        EmbeddedWebDropServer server = new(8089, group);

        // Act
        string html = server.GeneratePageHtml();

        // Assert
        html.Should().Contain("Warteschlange ist leer");
        html.Should().Contain("GNU AGPL-3.0");
    }
}
