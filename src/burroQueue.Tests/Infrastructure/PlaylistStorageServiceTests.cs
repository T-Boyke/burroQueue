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

    [Fact]
    public async Task LoadPlaylistAsync_WhenFileDoesNotExist_ShouldNotThrowOrModifyPlaylist()
    {
        PlaylistStorageService service = new(_tempFile);
        Playlist playlist = new();

        await service.LoadPlaylistAsync(playlist);

        playlist.Count.Should().Be(0);
    }

    [Fact]
    public async Task LoadPlaylistAsync_WhenFileContainsCorruptJson_ShouldHandleGracefully()
    {
        await File.WriteAllTextAsync(_tempFile, "{ this is not valid json :/");
        PlaylistStorageService service = new(_tempFile);
        Playlist playlist = new();

        await service.LoadPlaylistAsync(playlist);

        playlist.Count.Should().Be(0);
    }

    [Fact]
    public async Task LoadPlaylistAsync_WhenFileContainsEmptyList_ShouldNotModifyPlaylist()
    {
        await File.WriteAllTextAsync(_tempFile, "[]");
        PlaylistStorageService service = new(_tempFile);
        Playlist playlist = new();

        await service.LoadPlaylistAsync(playlist);

        playlist.Count.Should().Be(0);
    }

    [Fact]
    public async Task SavePlaylistAsync_WhenPlaylistIsNull_ShouldThrowArgumentNullException()
    {
        PlaylistStorageService service = new(_tempFile);

        Func<Task> act = async () => await service.SavePlaylistAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task LoadPlaylistAsync_WhenPlaylistIsNull_ShouldThrowArgumentNullException()
    {
        PlaylistStorageService service = new(_tempFile);

        Func<Task> act = async () => await service.LoadPlaylistAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithDefaultPath_ShouldInitializeSuccessfully()
    {
        PlaylistStorageService service = new();
        service.Should().NotBeNull();
    }

    [Fact]
    public async Task SavePlaylistAsync_WhenDirectoryDoesNotExist_ShouldCreateDirectory()
    {
        string subDir = Path.Combine(Path.GetTempPath(), $"burro_dir_{Guid.NewGuid():N}");
        string filePath = Path.Combine(subDir, "state.json");
        try
        {
            Directory.Exists(subDir).Should().BeFalse();
            PlaylistStorageService service = new(filePath);
            Playlist playlist = new();
            playlist.Enqueue(new Track("vid1", new Uri("https://youtube.com/watch?v=vid1"), "T1", "A1", DateTimeOffset.UtcNow));

            await service.SavePlaylistAsync(playlist);

            Directory.Exists(subDir).Should().BeTrue();
            File.Exists(filePath).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(subDir))
            {
                try { Directory.Delete(subDir, recursive: true); } catch { }
            }
        }
    }

    public void Dispose()
    {
        if (File.Exists(_tempFile))
        {
            File.Delete(_tempFile);
        }
    }
}


