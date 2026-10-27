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
}

