using burroQueue.Domain.Events;
using burroQueue.Domain.Model;
using FluentAssertions;
using Xunit;

namespace burroQueue.Tests.Domain;

public sealed class DomainEventsTests
{
    [Fact]
    public void PlaybackStateChangedDomainEvent_ShouldHoldExpectedProperties()
    {
        GroupId groupId = GroupId.New();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Track track = new("v123", new Uri("https://youtube.com/watch?v=v123"), "Test", "User", now);

        PlaybackStateChangedDomainEvent evt = new(groupId, PlaybackState.Stopped, PlaybackState.Playing, track, now);

        evt.GroupId.Should().Be(groupId);
        evt.PreviousState.Should().Be(PlaybackState.Stopped);
        evt.NewState.Should().Be(PlaybackState.Playing);
        evt.CurrentTrack.Should().Be(track);
        evt.OccurredAtUtc.Should().Be(now);
    }

    [Fact]
    public void TrackMetadataLoadedDomainEvent_ShouldHoldExpectedProperties()
    {
        TrackId trackId = TrackId.New();
        TrackMetadata metadata = new("Title", "Artist", new Uri("https://img.youtube.com/thumb.jpg"));
        DateTimeOffset now = DateTimeOffset.UtcNow;

        TrackMetadataLoadedDomainEvent evt = new(trackId, metadata, now);

        evt.TrackId.Should().Be(trackId);
        evt.Metadata.Should().Be(metadata);
        evt.OccurredAtUtc.Should().Be(now);
    }

    [Fact]
    public void TrackRemovedDomainEvent_ShouldHoldExpectedProperties()
    {
        TrackId trackId = TrackId.New();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        TrackRemovedDomainEvent evt = new(trackId, now);

        evt.TrackId.Should().Be(trackId);
        evt.OccurredAtUtc.Should().Be(now);
    }

    [Fact]
    public void VoteToSkipRecordedDomainEvent_ShouldHoldExpectedProperties()
    {
        TrackId trackId = TrackId.New();
        ClientId voterId = ClientId.New();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        VoteToSkipRecordedDomainEvent evt = new(trackId, voterId, 2, 3, now);

        evt.TrackId.Should().Be(trackId);
        evt.VoterId.Should().Be(voterId);
        evt.CurrentVotes.Should().Be(2);
        evt.RequiredVotes.Should().Be(3);
        evt.OccurredAtUtc.Should().Be(now);
    }

    [Fact]
    public void VoteToSkipThresholdReachedDomainEvent_ShouldHoldExpectedProperties()
    {
        TrackId trackId = TrackId.New();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        VoteToSkipThresholdReachedDomainEvent evt = new(trackId, 3, now);

        evt.TrackId.Should().Be(trackId);
        evt.TotalVotes.Should().Be(3);
        evt.OccurredAtUtc.Should().Be(now);
    }

    [Fact]
    public void CollaborativePlaylistLinkUpdatedDomainEvent_ShouldHoldExpectedProperties()
    {
        GroupId groupId = GroupId.New();
        CollaborativePlaylistLink link = new("https://www.youtube.com/playlist?list=PL123&jct=TOKEN");
        DateTimeOffset now = DateTimeOffset.UtcNow;

        CollaborativePlaylistLinkUpdatedDomainEvent evt = new(groupId, link, now);

        evt.GroupId.Should().Be(groupId);
        evt.NewLink.Should().BeSameAs(link);
        evt.OccurredAtUtc.Should().Be(now);
    }

    [Fact]
    public void GroupRenamedDomainEvent_ShouldHoldExpectedProperties()
    {
        GroupId groupId = GroupId.New();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        GroupRenamedDomainEvent evt = new(groupId, "Old Group", "New Group", now);

        evt.GroupId.Should().Be(groupId);
        evt.OldName.Should().Be("Old Group");
        evt.NewName.Should().Be("New Group");
        evt.OccurredAtUtc.Should().Be(now);
    }
}
