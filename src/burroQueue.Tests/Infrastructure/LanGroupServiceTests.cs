using System.Net;
using System.Net.Sockets;
using burroQueue.Domain.Model;
using burroQueue.Infrastructure.Network;
using FluentAssertions;
using Xunit;

namespace burroQueue.Tests.Infrastructure;

public sealed class LanGroupServiceTests : IAsyncLifetime, IDisposable
{
    private readonly LanGroupService _service;

    public LanGroupServiceTests()
    {
        _service = new LanGroupService();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await _service.DisposeAsync();
    }

    public void Dispose()
    {
        _service.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    [Fact]
    public async Task StartAndStopLifecycle_ShouldExecuteCleanly()
    {
        await _service.StartAsync();
        // Multiple calls to StartAsync should be idempotent
        await _service.StartAsync();

        await _service.StopAsync();
    }

    [Fact]
    public async Task BroadcastBeaconAsync_ShouldExecuteWithoutException()
    {
        await _service.StartAsync();
        BurroGroup group = new(GroupId.New(), "Test Office");
        ClientId hostId = ClientId.New();

        Func<Task> act = async () => await _service.BroadcastBeaconAsync(group, hostId, "HostName");
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SendPlaybackCommandAsync_ShouldExecuteWithoutException()
    {
        await _service.StartAsync();

        Func<Task> act = async () => await _service.SendPlaybackCommandAsync(GroupId.New(), ClientId.New(), "Play");
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SendGroupActionAsync_ShouldExecuteWithoutException()
    {
        await _service.StartAsync();

        Func<Task> act = async () => await _service.SendGroupActionAsync(GroupId.New(), ClientId.New(), "Rename", "NewName");
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ShareCollaborativePlaylistAsync_ShouldExecuteWithoutException()
    {
        await _service.StartAsync();

        Func<Task> act = async () => await _service.ShareCollaborativePlaylistAsync(
            GroupId.New(),
            "https://youtube.com/playlist?list=PL1&jct=ABC",
            "Sharer");
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SendQueueSyncAsync_ShouldExecuteWithoutException()
    {
        await _service.StartAsync();
        Track track = new("dQw4w9WgXcQ", new Uri("https://youtube.com/watch?v=dQw4w9WgXcQ"), "Rick", "Alice", DateTimeOffset.UtcNow);

        Func<Task> act = async () => await _service.SendQueueSyncAsync(GroupId.New(), "Enqueue", track);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SendSkipVoteAsync_ShouldExecuteWithoutException()
    {
        await _service.StartAsync();

        Func<Task> act = async () => await _service.SendSkipVoteAsync(GroupId.New(), "track-123", ClientId.New());
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SendMethods_WhenNotStarted_ShouldReturnGracefully()
    {
        LanGroupService unstarted = new();
        BurroGroup group = new(GroupId.New(), "Office");

        Func<Task> act1 = async () => await unstarted.BroadcastBeaconAsync(group, ClientId.New(), "User");
        Func<Task> act2 = async () => await unstarted.SendPlaybackCommandAsync(group.Id, ClientId.New(), "Play");
        Func<Task> act3 = async () => await unstarted.SendGroupActionAsync(group.Id, ClientId.New(), "Rename", "Name");
        Func<Task> act4 = async () => await unstarted.ShareCollaborativePlaylistAsync(group.Id, "url", "User");
        Func<Task> act5 = async () => await unstarted.SendSkipVoteAsync(group.Id, "track-1", ClientId.New());

        await act1.Should().NotThrowAsync();
        await act2.Should().NotThrowAsync();
        await act3.Should().NotThrowAsync();
        await act4.Should().NotThrowAsync();
        await act5.Should().NotThrowAsync();
    }

    [Fact]
    public async Task StopAsync_WhenCalledMultipleTimes_ShouldBeIdempotent()
    {
        await _service.StartAsync();
        await _service.StopAsync();
        Func<Task> act = async () => await _service.StopAsync();
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DisposeAsync_WhenCalledMultipleTimes_ShouldBeIdempotent()
    {
        LanGroupService svc = new();
        await svc.DisposeAsync();
        Func<Task> act = async () => await svc.DisposeAsync();
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task MulticastListener_WhenMalformedPacketReceived_ShouldNotThrow()
    {
        await _service.StartAsync();

        using UdpClient sender = new();
        byte[] malformed = "not valid json"u8.ToArray();
        await sender.SendAsync(malformed, malformed.Length, new IPEndPoint(IPAddress.Loopback, 41234));

        byte[] missingFields = """{"Invalid": 123}"""u8.ToArray();
        await sender.SendAsync(missingFields, missingFields.Length, new IPEndPoint(IPAddress.Loopback, 41234));

        byte[] unknownType = """{"Type":"UNKNOWN_TYPE","Data":{}}"""u8.ToArray();
        await sender.SendAsync(unknownType, unknownType.Length, new IPEndPoint(IPAddress.Loopback, 41234));

        await Task.Delay(50);
    }

    [Fact]
    public async Task BroadcastBeaconAsync_WithNullGroup_ShouldThrowArgumentNullException()
    {
        Func<Task> act = async () => await _service.BroadcastBeaconAsync(null!, ClientId.New(), "Host");
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task SendQueueSyncAsync_WithNullTrack_ShouldThrowArgumentNullException()
    {
        Func<Task> act = async () => await _service.SendQueueSyncAsync(GroupId.New(), "Enqueue", null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task StartAsync_WhenSocketExceptionThrown_ShouldHandleGracefully()
    {
        LanGroupService service = new();
        service.UdpClientFactory = () => throw new System.Net.Sockets.SocketException((int)System.Net.Sockets.SocketError.AddressAlreadyInUse);

        Func<Task> act = async () => await service.StartAsync();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SendMessageAsync_WhenSocketThrowsException_ShouldCatchGracefully()
    {
        LanGroupService service = new();
        await service.StartAsync();
        service.UdpClient?.Client.Close();

        Func<Task> act = async () => await service.SendQueueSyncAsync(GroupId.New(), "Enqueue", new Track("vid", new Uri("https://youtube.com/watch?v=vid"), "T", "A", DateTimeOffset.UtcNow));

        await act.Should().NotThrowAsync();
        await service.StopAsync();
    }

    [Fact]
    public async Task DiscoveryProbe_WhenReceivedByHost_ShouldReplyWithDirectUnicastBeacon()
    {
        const int testPort = 41987;
        BurroGroup hostGroup = new(GroupId.New(), "Engineering Floor 3");
        ClientId hostClientId = ClientId.New();

        await using LanGroupService host = new() { Port = testPort };
        host.SetBeaconResponder(() => new burroQueue.Infrastructure.Network.Messages.DiscoveryBeaconMessage(
            hostGroup.Id.Value,
            hostGroup.Name,
            hostClientId.Value,
            "Host-PC",
            MemberCount: 2,
            HasCollaborativePlaylist: false,
            CollaborativePlaylistUrl: null,
            DateTimeOffset.UtcNow));
        await host.StartAsync();

        // Simulate Client B sending DISCOVER_PROBE from an ephemeral UDP socket and awaiting direct unicast BEACON reply
        using UdpClient clientSocket = new(new IPEndPoint(IPAddress.Loopback, 0));
        string probeJson = System.Text.Json.JsonSerializer.Serialize(new
        {
            Type = "DISCOVER_PROBE",
            Data = new burroQueue.Infrastructure.Network.Messages.DiscoveryProbeMessage(
                Guid.NewGuid(),
                "Client-B",
                DateTimeOffset.UtcNow)
        });
        byte[] probeBytes = System.Text.Encoding.UTF8.GetBytes(probeJson);
        await clientSocket.SendAsync(probeBytes, probeBytes.Length, new IPEndPoint(IPAddress.Loopback, testPort));

        UdpReceiveResult reply = await clientSocket.ReceiveAsync().WaitAsync(TimeSpan.FromSeconds(2));
        string replyJson = System.Text.Encoding.UTF8.GetString(reply.Buffer);

        replyJson.Should().Contain("\"Type\":\"BEACON\"");
        replyJson.Should().Contain("Engineering Floor 3");
        replyJson.Should().Contain("Host-PC");
    }

    [Fact]
    public async Task SendTelephonyStateAsync_WhenReceived_ShouldRaiseTelephonyStateReceivedEvent()
    {
        const int testPort = 41988;
        await using LanGroupService receiver = new() { Port = testPort };
        TaskCompletionSource<burroQueue.Infrastructure.Network.Messages.PeerTelephonyStateMessage> tcs = new();
        receiver.TelephonyStateReceived += (_, msg) => tcs.TrySetResult(msg);
        await receiver.StartAsync();

        GroupId gid = GroupId.New();
        ClientId peerId = ClientId.New();
        using UdpClient sender = new();
        string packet = System.Text.Json.JsonSerializer.Serialize(new
        {
            Type = "PEER_TELEPHONY",
            Data = new burroQueue.Infrastructure.Network.Messages.PeerTelephonyStateMessage(
                gid.Value,
                peerId.Value,
                "Sabine",
                "Starface",
                IsCallActive: true,
                DateTimeOffset.UtcNow)
        });
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(packet);
        await sender.SendAsync(bytes, bytes.Length, new IPEndPoint(IPAddress.Loopback, testPort));

        var received = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(2));
        received.GroupId.Should().Be(gid.Value);
        received.ClientDisplayName.Should().Be("Sabine");
        received.ProviderId.Should().Be("Starface");
        received.IsCallActive.Should().BeTrue();
    }
}



