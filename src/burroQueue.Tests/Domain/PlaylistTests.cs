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

    private static Track CreateSampleTrack(string videoId, string title) =>
        new(videoId, new Uri($"https://www.youtube.com/watch?v={videoId}"), title, "Tester", DateTimeOffset.UtcNow);
}

