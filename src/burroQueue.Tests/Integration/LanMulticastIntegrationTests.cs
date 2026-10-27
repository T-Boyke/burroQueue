using burroQueue.Domain.Model;
using burroQueue.Infrastructure.Network;
using burroQueue.Infrastructure.Network.Messages;
using FluentAssertions;
using Xunit;

namespace burroQueue.Tests.Integration;

public sealed class LanMulticastIntegrationTests : IAsyncLifetime, IDisposable
{
    private LanGroupService? _sender;
    private LanGroupService? _receiver;

    public async Task InitializeAsync()
    {
        _sender = new LanGroupService();
        _receiver = new LanGroupService();

        await _sender.StartAsync();
        await _receiver.StartAsync();
    }

    public async Task DisposeAsync()
    {
        if (_sender is not null)
        {
            await _sender.DisposeAsync();
            _sender = null;
        }

        if (_receiver is not null)
        {
            await _receiver.DisposeAsync();
            _receiver = null;
        }
    }

    public void Dispose()
    {
        DisposeAsync().GetAwaiter().GetResult();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task BroadcastBeacon_ShouldBeReceivedByPeer()
    {
        GroupId groupId = GroupId.New();
        TaskCompletionSource<DiscoveryBeaconMessage> tcs = new();

        _receiver!.GroupDiscovered += (_, msg) =>
        {
            if (msg.GroupId == groupId.Value)
            {
                tcs.TrySetResult(msg);
            }
        };

        BurroGroup group = new(groupId, "Beacon Office");
        ClientId hostId = ClientId.New();
        await _sender!.BroadcastBeaconAsync(group, hostId, "Alice");

        Task completed = await Task.WhenAny(tcs.Task, Task.Delay(1500));
        if (completed == tcs.Task)
        {
            DiscoveryBeaconMessage received = await tcs.Task;
            received.GroupName.Should().Be("Beacon Office");
            received.HostDisplayName.Should().Be("Alice");
            received.MemberCount.Should().Be(0);
        }
    }

    [Fact]
    public async Task SendQueueSync_ShouldBeReceivedByPeer()
    {
        GroupId groupId = GroupId.New();
        TaskCompletionSource<QueueSyncMessage> tcs = new();

        _receiver!.QueueSyncReceived += (_, msg) =>
        {
            if (msg.GroupId == groupId.Value)
            {
                tcs.TrySetResult(msg);
            }
        };

        Track track = new("syncVideo1", new Uri("https://www.youtube.com/watch?v=syncVideo1"), "Sync Song", "Bob", DateTimeOffset.UtcNow);
        await _sender!.SendQueueSyncAsync(groupId, "Enqueue", track);

        Task completed = await Task.WhenAny(tcs.Task, Task.Delay(1500));
        if (completed == tcs.Task)
        {
            QueueSyncMessage received = await tcs.Task;
            received.Action.Should().Be("Enqueue");
            received.VideoId.Should().Be("syncVideo1");
            received.Title.Should().Be("Sync Song");
        }
    }

    [Fact]
    public async Task ShareCollaborativePlaylist_ShouldBeReceivedByPeer()
    {
        GroupId groupId = GroupId.New();
        TaskCompletionSource<SharedPlaylistSyncMessage> tcs = new();

        _receiver!.CollaborativePlaylistReceived += (_, msg) =>
        {
            if (msg.GroupId == groupId.Value)
            {
                tcs.TrySetResult(msg);
            }
        };

        await _sender!.ShareCollaborativePlaylistAsync(groupId, "https://youtube.com/playlist?list=123", "Alice");

        Task completed = await Task.WhenAny(tcs.Task, Task.Delay(1500));
        if (completed == tcs.Task)
        {
            SharedPlaylistSyncMessage received = await tcs.Task;
            received.CollaborativePlaylistUrl.Should().Be("https://youtube.com/playlist?list=123");
            received.SharedByDisplayName.Should().Be("Alice");
        }
    }

    [Fact]
    public async Task SendGroupAction_ShouldBeReceivedByPeer()
    {
        GroupId groupId = GroupId.New();
        ClientId actorId = ClientId.New();
        TaskCompletionSource<GroupActionMessage> tcs = new();

        _receiver!.GroupActionReceived += (_, msg) =>
        {
            if (msg.GroupId == groupId.Value)
            {
                tcs.TrySetResult(msg);
            }
        };

        await _sender!.SendGroupActionAsync(groupId, actorId, "Rename", "Office Fun");

        Task completed = await Task.WhenAny(tcs.Task, Task.Delay(1500));
        if (completed == tcs.Task)
        {
            GroupActionMessage received = await tcs.Task;
            received.ActionType.Should().Be("Rename");
            received.Payload.Should().Be("Office Fun");
        }
    }
}
