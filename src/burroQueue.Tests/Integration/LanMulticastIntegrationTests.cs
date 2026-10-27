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
}
