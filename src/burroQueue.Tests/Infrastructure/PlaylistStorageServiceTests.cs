using burroQueue.Domain.Model;
using burroQueue.Infrastructure.Persistence;
using FluentAssertions;
using Xunit;

namespace burroQueue.Tests.Infrastructure;

public sealed class PlaylistStorageServiceTests : IDisposable
{
    private readonly string _tempFile;

    public PlaylistStorageServiceTests()
    {
        _tempFile = Path.Combine(Path.GetTempPath(), $"burro_test_storage_{Guid.NewGuid():N}.json");
    }

    [Fact]
    public async Task SaveAndLoadPlaylist_ShouldRestoreTracksAccurately()
    {
        PlaylistStorageService service = new(_tempFile);
        Playlist originalPlaylist = new();

        Track t1 = new("dQw4w9WgXcQ", new Uri("https://www.youtube.com/watch?v=dQw4w9WgXcQ"), "Song 1", "Alice", DateTimeOffset.UtcNow);
        Track t2 = new("e-ORhEE9VVg", new Uri("https://www.youtube.com/watch?v=e-ORhEE9VVg"), "Song 2", "Bob", DateTimeOffset.UtcNow);

        originalPlaylist.Enqueue(t1);
        originalPlaylist.Enqueue(t2);

        await service.SavePlaylistAsync(originalPlaylist);

        Playlist restoredPlaylist = new();
        await service.LoadPlaylistAsync(restoredPlaylist);

        restoredPlaylist.CurrentTrack.Should().NotBeNull();
        restoredPlaylist.CurrentTrack!.VideoId.Should().Be("dQw4w9WgXcQ");
        restoredPlaylist.QueuedTracks.Should().ContainSingle()
            .Which.VideoId.Should().Be("e-ORhEE9VVg");
    }

    public void Dispose()
    {
        if (File.Exists(_tempFile))
        {
            File.Delete(_tempFile);
        }
    }
}
