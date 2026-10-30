using burroQueue.Domain.Model;
using burroQueue.Domain.Services;
using burroQueue.Infrastructure.Cryptography;
using FluentAssertions;
using Xunit;

namespace burroQueue.Tests.Domain;

public sealed class QueueShuffleEngineTests
{
    [Fact]
    public void EnableAndDisableShuffle_ShouldPinActiveTrackAtZeroShuffleRemainingAndRestoreCanonicalOrder()
    {
        Playlist playlist = new();
        Track t1 = CreateTrack("vid1", "Song 1");
        Track t2 = CreateTrack("vid2", "Song 2");
        Track t3 = CreateTrack("vid3", "Song 3");
        Track t4 = CreateTrack("vid4", "Song 4");
        playlist.Enqueue(t1);
        playlist.Enqueue(t2);
        playlist.Enqueue(t3);
        playlist.Enqueue(t4);

        // Currently playing t2 (index 1)
        playlist.JumpToTrack(t2.Id);

        QueueShuffleEngine engine = new();
        DeterministicTestEntropySource entropy = new();

        engine.EnableShuffle(playlist, entropy);

        engine.IsShuffleEnabled.Should().BeTrue();
        playlist.CurrentTrackIndex.Should().Be(0);
        playlist.CurrentTrack.Should().BeSameAs(t2);
        // Remaining [t1, t3, t4] reversed by DeterministicTestEntropySource -> [t4, t3, t1]
        playlist.AllTracks.Should().Equal(t2, t4, t3, t1);

        // Disable shuffle -> canonical order [t1, t2, t3, t4] restored with cursor still on t2!
        engine.DisableShuffle(playlist);

        engine.IsShuffleEnabled.Should().BeFalse();
        playlist.AllTracks.Should().Equal(t1, t2, t3, t4);
        playlist.CurrentTrack.Should().BeSameAs(t2);
        playlist.CurrentTrackIndex.Should().Be(1);
    }

    [Fact]
    public void EnableShuffle_WithRecentBoundaryTrack_ShouldPreventImmediateRepeatAcrossCycles()
    {
        Playlist playlist = new();
        Track t1 = CreateTrack("vid1", "Song 1");
        Track t2 = CreateTrack("vid2", "Song 2");
        Track t3 = CreateTrack("vid3", "Song 3");
        playlist.Enqueue(t1);
        playlist.Enqueue(t2);
        playlist.Enqueue(t3);

        QueueShuffleEngine engine = new();
        // Record vid3 as the most recently played boundary track
        engine.RecordPlayed("vid3");

        // Reversing [t2, t3] puts vid3 first ([t3, t2]), which must be swapped by anti-repeat guard -> [t2, t3]
        engine.EnableShuffle(playlist, new DeterministicTestEntropySource());

        playlist.QueuedTracks[0].VideoId.Should().NotBe("vid3");
        playlist.QueuedTracks[0].VideoId.Should().Be("vid2");
    }

    [Fact]
    public void HardwareCsprngEntropySource_ShouldPermuteSpanWithoutLosingElements()
    {
        HardwareCsprngEntropySource csprng = new();
        int[] values = Enumerable.Range(1, 32).ToArray();

        csprng.Shuffle(values.AsSpan());

        values.Should().HaveCount(32);
        values.Should().BeEquivalentTo(Enumerable.Range(1, 32));
        int sample = csprng.GetNextInt32(10, 20);
        sample.Should().BeInRange(10, 19);
    }

    private static Track CreateTrack(string id, string title) =>
        new(id, new Uri($"https://www.youtube.com/watch?v={id}"), title, "Tester", DateTimeOffset.UtcNow);
}
