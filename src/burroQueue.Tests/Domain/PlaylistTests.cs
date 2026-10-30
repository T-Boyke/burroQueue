using burroQueue.Domain.Model;
using FluentAssertions;
using Xunit;

namespace burroQueue.Tests.Domain;

public sealed class PlaylistTests
{
    [Fact]
    public void Enqueue_WhenPlaylistIsEmpty_ShouldSetCurrentTrackImmediately()
    {
        Playlist playlist = new();
        Track firstTrack = CreateSampleTrack("dQw4w9WgXcQ", "First Track");

        playlist.Enqueue(firstTrack);

        playlist.CurrentTrack.Should().BeSameAs(firstTrack);
        playlist.Count.Should().Be(0);
    }

    [Fact]
    public void Enqueue_WhenCurrentTrackExists_ShouldAppendToFifoQueue()
    {
        Playlist playlist = new();
        Track first = CreateSampleTrack("11111111111", "First");
        Track second = CreateSampleTrack("22222222222", "Second");
        Track third = CreateSampleTrack("33333333333", "Third");

        playlist.Enqueue(first);
        playlist.Enqueue(second);
        playlist.Enqueue(third);

        playlist.CurrentTrack.Should().BeSameAs(first);
        playlist.Count.Should().Be(2);

        Track? next = playlist.AdvanceNext();
        next.Should().BeSameAs(second);
        playlist.CurrentTrack.Should().BeSameAs(second);
        playlist.Count.Should().Be(1);
    }

    [Fact]
    public void Remove_WhenTrackInQueue_ShouldRemoveTrackAndDecrementCount()
    {
        Playlist playlist = new();
        Track first = CreateSampleTrack("11111111111", "First");
        Track second = CreateSampleTrack("22222222222", "Second");
        Track third = CreateSampleTrack("33333333333", "Third");

        playlist.Enqueue(first);
        playlist.Enqueue(second);
        playlist.Enqueue(third);

        bool removed = playlist.Remove(second.Id);

        removed.Should().BeTrue();
        playlist.Count.Should().Be(1);
        playlist.QueuedTracks.Should().ContainSingle().Which.Should().BeSameAs(third);
    }

    [Fact]
    public void Move_WhenValidIndices_ShouldReorderQueue()
    {
        Playlist playlist = new();
        Track current = CreateSampleTrack("00000000000", "Current");
        Track t1 = CreateSampleTrack("11111111111", "Song 1");
        Track t2 = CreateSampleTrack("22222222222", "Song 2");

        playlist.Enqueue(current);
        playlist.Enqueue(t1);
        playlist.Enqueue(t2);

        bool moved = playlist.Move(0, 1);

        moved.Should().BeTrue();
        playlist.QueuedTracks[0].Should().BeSameAs(t2);
        playlist.QueuedTracks[1].Should().BeSameAs(t1);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    [InlineData(0, 5)]
    [InlineData(5, 0)]
    [InlineData(0, 0)]
    public void Move_WhenInvalidIndices_ShouldReturnFalse(int fromIndex, int toIndex)
    {
        Playlist playlist = new();
        Track current = CreateSampleTrack("00000000000", "Current");
        Track t1 = CreateSampleTrack("11111111111", "Song 1");
        playlist.Enqueue(current);
        playlist.Enqueue(t1);

        bool moved = playlist.Move(fromIndex, toIndex);
        moved.Should().BeFalse();
    }

    [Fact]
    public void Remove_WhenTrackNotInQueue_ShouldReturnFalse()
    {
        Playlist playlist = new();
        Track current = CreateSampleTrack("00000000000", "Current");
        playlist.Enqueue(current);

        bool removed = playlist.Remove(TrackId.New());
        removed.Should().BeFalse();
    }

    [Fact]
    public void AdvanceNext_WhenQueueEmpty_ShouldSetCurrentTrackToNull()
    {
        Playlist playlist = new();
        Track current = CreateSampleTrack("00000000000", "Current");
        playlist.Enqueue(current);

        Track? next = playlist.AdvanceNext();
        next.Should().BeNull();
        playlist.CurrentTrack.Should().BeNull();
    }

    [Fact]
    public void Clear_ShouldEmptyQueueAndResetCurrentTrack()
    {
        Playlist playlist = new();
        playlist.Enqueue(CreateSampleTrack("11111111111", "Track 1"));
        playlist.Enqueue(CreateSampleTrack("22222222222", "Track 2"));

        playlist.Clear();

        playlist.CurrentTrack.Should().BeNull();
        playlist.QueuedTracks.Should().BeEmpty();
        playlist.Count.Should().Be(0);
    }

    [Fact]
    public void Enqueue_WhenTrackNull_ShouldThrowArgumentNullException()
    {
        Playlist playlist = new();
        Action act = () => playlist.Enqueue(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void MoveUp_WhenFirstItem_ShouldReturnFalse()
    {
        Playlist playlist = new();
        Track current = CreateSampleTrack("00000000000", "Current");
        Track t1 = CreateSampleTrack("11111111111", "Song 1");
        Track t2 = CreateSampleTrack("22222222222", "Song 2");

        playlist.Enqueue(current);
        playlist.Enqueue(t1);
        playlist.Enqueue(t2);

        bool moved = playlist.MoveUp(t1.Id);

        moved.Should().BeFalse();
        playlist.QueuedTracks[0].Should().BeSameAs(t1);
        playlist.QueuedTracks[1].Should().BeSameAs(t2);
    }

    [Fact]
    public void MoveUp_WhenItemInMiddle_ShouldSwapWithPrevious()
    {
        Playlist playlist = new();
        Track current = CreateSampleTrack("00000000000", "Current");
        Track t1 = CreateSampleTrack("11111111111", "Song 1");
        Track t2 = CreateSampleTrack("22222222222", "Song 2");
        Track t3 = CreateSampleTrack("33333333333", "Song 3");

        playlist.Enqueue(current);
        playlist.Enqueue(t1);
        playlist.Enqueue(t2);
        playlist.Enqueue(t3);

        bool moved = playlist.MoveUp(t2.Id);

        moved.Should().BeTrue();
        playlist.QueuedTracks[0].Should().BeSameAs(t2);
        playlist.QueuedTracks[1].Should().BeSameAs(t1);
        playlist.QueuedTracks[2].Should().BeSameAs(t3);
    }

    [Fact]
    public void MoveUp_WhenTrackNotFound_ShouldReturnFalse()
    {
        Playlist playlist = new();
        Track current = CreateSampleTrack("00000000000", "Current");
        Track t1 = CreateSampleTrack("11111111111", "Song 1");
        playlist.Enqueue(current);
        playlist.Enqueue(t1);

        bool moved = playlist.MoveUp(TrackId.New());

        moved.Should().BeFalse();
    }

    [Fact]
    public void MoveDown_WhenLastItem_ShouldReturnFalse()
    {
        Playlist playlist = new();
        Track current = CreateSampleTrack("00000000000", "Current");
        Track t1 = CreateSampleTrack("11111111111", "Song 1");
        Track t2 = CreateSampleTrack("22222222222", "Song 2");

        playlist.Enqueue(current);
        playlist.Enqueue(t1);
        playlist.Enqueue(t2);

        bool moved = playlist.MoveDown(t2.Id);

        moved.Should().BeFalse();
        playlist.QueuedTracks[0].Should().BeSameAs(t1);
        playlist.QueuedTracks[1].Should().BeSameAs(t2);
    }

    [Fact]
    public void MoveDown_WhenItemInMiddle_ShouldSwapWithNext()
    {
        Playlist playlist = new();
        Track current = CreateSampleTrack("00000000000", "Current");
        Track t1 = CreateSampleTrack("11111111111", "Song 1");
        Track t2 = CreateSampleTrack("22222222222", "Song 2");
        Track t3 = CreateSampleTrack("33333333333", "Song 3");

        playlist.Enqueue(current);
        playlist.Enqueue(t1);
        playlist.Enqueue(t2);
        playlist.Enqueue(t3);

        bool moved = playlist.MoveDown(t2.Id);

        moved.Should().BeTrue();
        playlist.QueuedTracks[0].Should().BeSameAs(t1);
        playlist.QueuedTracks[1].Should().BeSameAs(t3);
        playlist.QueuedTracks[2].Should().BeSameAs(t2);
    }

    [Fact]
    public void MoveDown_WhenTrackNotFound_ShouldReturnFalse()
    {
        Playlist playlist = new();
        Track current = CreateSampleTrack("00000000000", "Current");
        Track t1 = CreateSampleTrack("11111111111", "Song 1");
        playlist.Enqueue(current);
        playlist.Enqueue(t1);

        bool moved = playlist.MoveDown(TrackId.New());

        moved.Should().BeFalse();
    }

    [Fact]
    public void ReconcileRemotePlaylist_ShouldAddNewPruneDeletedSyncedTracksKeepManualAndCurrentTrack()
    {
        Playlist playlist = new();
        Track activeSynced = new("active11111", new Uri("https://www.youtube.com/watch?v=active11111"), "Active Song", "Alice", DateTimeOffset.UtcNow, isSyncedFromPlaylist: true);
        Track deletedSynced = new("deleted2222", new Uri("https://www.youtube.com/watch?v=deleted2222"), "Deleted On YT", "Bob", DateTimeOffset.UtcNow, isSyncedFromPlaylist: true);
        Track keptSynced = new("kept3333333", new Uri("https://www.youtube.com/watch?v=kept3333333"), "Still On YT", "OldChannel", DateTimeOffset.UtcNow, isSyncedFromPlaylist: true);
        Track manualLocal = new("manual44444", new Uri("https://www.youtube.com/watch?v=manual44444"), "Local DragDrop", "Charlie", DateTimeOffset.UtcNow, isSyncedFromPlaylist: false);

        playlist.Enqueue(activeSynced);
        playlist.Enqueue(deletedSynced);
        playlist.Enqueue(keptSynced);
        playlist.Enqueue(manualLocal);

        Uri avatarUri = new("https://yt3.ggpht.com/ytc/avatar_clara=s88-c-k");
        Track remoteKeptUpdated = new("kept3333333", new Uri("https://www.youtube.com/watch?v=kept3333333"), "Still On YT", "Clara", DateTimeOffset.UtcNow, artistName: "Daft Punk", submitterAvatarUrl: avatarUri, isSyncedFromPlaylist: true);
        Track remoteNew = new("new55555555", new Uri("https://www.youtube.com/watch?v=new55555555"), "Brand New YT", "Tobias", DateTimeOffset.UtcNow, artistName: "Gorillaz", submitterAvatarUrl: avatarUri, isSyncedFromPlaylist: true);

        PlaylistReconciliationResult result = playlist.ReconcileRemotePlaylist([remoteKeptUpdated, remoteNew]);

        // Active track is never cut off even if deleted remotely
        playlist.CurrentTrack.Should().BeSameAs(activeSynced);
        // Deleted synced track was pruned
        result.RemovedTracks.Should().ContainSingle(t => t.VideoId == "deleted2222");
        // New remote track was added
        result.AddedTracks.Should().ContainSingle(t => t.VideoId == "new55555555");
        // Existing synced track received enriched collaborator & avatar attribution
        result.UpdatedAttributionCount.Should().Be(1);
        keptSynced.SubmittedBy.Should().Be("Clara");
        keptSynced.ArtistName.Should().Be("Daft Punk");
        keptSynced.SubmitterAvatarUrl.Should().Be(avatarUri);
        // Manual drag-drop track was preserved untouched
        playlist.QueuedTracks.Select(t => t.VideoId).Should().Equal("kept3333333", "manual44444", "new55555555");
    }

    private static Track CreateSampleTrack(string videoId, string title) =>
        new(videoId, new Uri($"https://www.youtube.com/watch?v={videoId}"), title, "Tester", DateTimeOffset.UtcNow);
}

