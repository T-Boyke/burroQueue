using burroQueue.Domain.Model;
using burroQueue.Infrastructure.Persistence;
using FluentAssertions;
using Xunit;

namespace burroQueue.Tests.Infrastructure;

public sealed class TomlFavoriteQueueRepositoryTests : IDisposable
{
    private readonly string _tempDir;

    public TomlFavoriteQueueRepositoryTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"burro_favs_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }
        catch
        {
        }
    }

    [Fact]
    public async Task SaveAndLoadFavoritesAsync_ShouldWriteGroupFavoritesTomlWithLikedByArrayAndRoundTrip()
    {
        TomlFavoriteQueueRepository repo = new(_tempDir);
        FavoriteTrackEntry entry1 = new("dQw4w9WgXcQ", new Uri("https://www.youtube.com/watch?v=dQw4w9WgXcQ"), "Never Gonna Give You Up", ["alice", "bob", "tobias"]);
        FavoriteTrackEntry entry2 = new("5NV6Rdv1a3I", new Uri("https://www.youtube.com/watch?v=5NV6Rdv1a3I"), "Get Lucky", ["clara"]);

        await repo.SaveFavoritesAsync("Office Engineering", [entry1, entry2]);

        string expectedFile = Path.Combine(_tempDir, "favorites.office-engineering.toml");
        File.Exists(expectedFile).Should().BeTrue();
        string rawToml = await File.ReadAllTextAsync(expectedFile);
        rawToml.Should().Contain("[group]");
        rawToml.Should().Contain("[[tracks]]");
        rawToml.Should().Contain("liked_by = [\"alice\", \"bob\", \"tobias\"]");

        IReadOnlyList<FavoriteTrackEntry> loaded = await repo.LoadFavoritesAsync("Office Engineering");

        loaded.Should().HaveCount(2);
        loaded[0].VideoId.Should().Be("dQw4w9WgXcQ");
        loaded[0].Likes.Should().Be(3);
        loaded[0].LikedBy.Should().Equal("alice", "bob", "tobias");
        loaded[1].VideoId.Should().Be("5NV6Rdv1a3I");
        loaded[1].Likes.Should().Be(1);
    }
}

