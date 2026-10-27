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

    private static Track CreateSampleTrack(string videoId, string title) =>
        new(videoId, new Uri($"https://www.youtube.com/watch?v={videoId}"), title, "Tester", DateTimeOffset.UtcNow);
}
