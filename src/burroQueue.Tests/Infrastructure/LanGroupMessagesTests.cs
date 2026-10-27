using System.Text.Json;
using burroQueue.Infrastructure.Network.Messages;
using FluentAssertions;
using Xunit;

namespace burroQueue.Tests.Infrastructure;

public sealed class LanGroupMessagesTests
{
    [Fact]
    public void DiscoveryBeaconMessage_ShouldSerializeAndDeserializeAccurately()
    {
        Guid groupId = Guid.NewGuid();
        Guid hostId = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        DiscoveryBeaconMessage original = new(
            groupId,
            "Office Jukebox",
            hostId,
            "Bob",
            3,
            true,
            "https://www.youtube.com/playlist?list=PL123&jct=ABC",
            now);

        string json = JsonSerializer.Serialize(original);
        DiscoveryBeaconMessage? deserialized = JsonSerializer.Deserialize<DiscoveryBeaconMessage>(json);

        deserialized.Should().NotBeNull();
        deserialized!.GroupId.Should().Be(groupId);
        deserialized.GroupName.Should().Be("Office Jukebox");
        deserialized.HostDisplayName.Should().Be("Bob");
        deserialized.HasCollaborativePlaylist.Should().BeTrue();
        deserialized.CollaborativePlaylistUrl.Should().Be("https://www.youtube.com/playlist?list=PL123&jct=ABC");
    }

    [Fact]
    public void SharedPlaylistSyncMessage_ShouldPreserveTokenAndLink()
    {
        Guid groupId = Guid.NewGuid();
        SharedPlaylistSyncMessage original = new(groupId, "https://youtube.com/playlist?list=XYZ&jct=TOKEN", "Alice", DateTimeOffset.UtcNow);

        string json = JsonSerializer.Serialize(original);
        SharedPlaylistSyncMessage? deserialized = JsonSerializer.Deserialize<SharedPlaylistSyncMessage>(json);

        deserialized.Should().NotBeNull();
        deserialized!.CollaborativePlaylistUrl.Should().Be("https://youtube.com/playlist?list=XYZ&jct=TOKEN");
        deserialized.SharedByDisplayName.Should().Be("Alice");
    }

    [Fact]
    public void QueueSyncMessage_ShouldSerializeAndDeserializeProperly()
    {
        Guid groupId = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        QueueSyncMessage original = new(groupId, "Enqueue", "abc12345678", "https://www.youtube.com/watch?v=abc12345678", "Super Song", "UserX", now);

        string json = JsonSerializer.Serialize(original);
        QueueSyncMessage? deserialized = JsonSerializer.Deserialize<QueueSyncMessage>(json);

        deserialized.Should().NotBeNull();
        deserialized!.GroupId.Should().Be(groupId);
        deserialized.Action.Should().Be("Enqueue");
        deserialized.VideoId.Should().Be("abc12345678");
        deserialized.Title.Should().Be("Super Song");
        deserialized.AddedBy.Should().Be("UserX");
    }

    [Fact]
    public void SkipVoteMessage_ShouldSerializeAndDeserializeProperly()
    {
        Guid groupId = Guid.NewGuid();
        Guid voterId = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        SkipVoteMessage original = new(groupId, "track-99", voterId, now);

        string json = JsonSerializer.Serialize(original);
        SkipVoteMessage? deserialized = JsonSerializer.Deserialize<SkipVoteMessage>(json);

        deserialized.Should().NotBeNull();
        deserialized!.GroupId.Should().Be(groupId);
        deserialized.TrackId.Should().Be("track-99");
        deserialized.VoterClientId.Should().Be(voterId);
    }
}

