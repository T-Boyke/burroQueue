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
}


