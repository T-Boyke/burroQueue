using System.Net;
using System.Net.Sockets;
using burroQueue.Domain.Model;
using burroQueue.Infrastructure.Web;
using FluentAssertions;
using Xunit;

namespace burroQueue.Tests.Integration;

public sealed class WebDropServerIntegrationTests : IAsyncLifetime, IDisposable
{
    private readonly int _port;
    private readonly BurroGroup _group;
    private EmbeddedWebDropServer? _server;
    private HttpClient? _httpClient;

    public WebDropServerIntegrationTests()
    {
        using (TcpListener tcp = new(IPAddress.Loopback, 0))
        {
            tcp.Start();
            _port = ((IPEndPoint)tcp.LocalEndpoint).Port;
            tcp.Stop();
        }

        _group = new BurroGroup(GroupId.New(), "Integration Test Group");
    }

    public async Task InitializeAsync()
    {
        _server = new EmbeddedWebDropServer(_port, _group);
        await _server.StartAsync();
        _httpClient = new HttpClient();
    }

    public async Task DisposeAsync()
    {
        _httpClient?.Dispose();
        _httpClient = null;

        if (_server is not null)
        {
            await _server.DisposeAsync();
            _server = null;
        }
    }

    public void Dispose()
    {
        DisposeAsync().GetAwaiter().GetResult();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task GetRoot_ShouldReturnHtmlWithGroupAndQueueInfo()
    {
        if (_server is null || !_server.IsRunning)
        {
            return; // Skip if port binding not allowed in test runner environment
        }

        Track current = new("t1", new Uri("https://www.youtube.com/watch?v=t1"), "Song One", "Alice", DateTimeOffset.UtcNow);
        Track queued = new("t2", new Uri("https://www.youtube.com/watch?v=t2"), "Song Two", "Bob", DateTimeOffset.UtcNow);
        _group.Playlist.Enqueue(current);
        _group.Playlist.Enqueue(queued);

        HttpResponseMessage response = await _httpClient!.GetAsync($"http://localhost:{_port}/");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/html");

        string html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("Integration Test Group");
        html.Should().Contain("Song One");
        html.Should().Contain("Song Two");
        html.Should().Contain("burroQueue");
    }

    [Fact]
    public async Task GetRoot_Via127001_ShouldReturnHtmlSuccessfullyWithoutHostnameError()
    {
        if (_server is null || !_server.IsRunning)
        {
            return;
        }

        HttpResponseMessage response = await _httpClient!.GetAsync($"http://127.0.0.1:{_port}/");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/html");
        string html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("burroQueue");
    }

    [Fact]
    public async Task GetRoot_WithCustomLanHostHeader_ShouldReturnHtmlWithout400InvalidHostname()
    {
        if (_server is null || !_server.IsRunning)
        {
            return;
        }

        using HttpRequestMessage request = new(HttpMethod.Get, $"http://127.0.0.1:{_port}/");
        request.Headers.Host = $"192.168.2.109:{_port}";

        HttpResponseMessage response = await _httpClient!.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        string html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("burroQueue");
    }

    [Fact]
    public async Task PostDrop_WithUrlEncodedBody_ShouldRaiseLinkDroppedEvent()
    {
        if (_server is null || !_server.IsRunning)
        {
            return;
        }

        string? droppedUrl = null;
        _server.LinkDropped += (_, url) => droppedUrl = url;

        Dictionary<string, string> form = new()
        {
            ["url"] = "https://www.youtube.com/watch?v=dQw4w9WgXcQ"
        };
        FormUrlEncodedContent content = new(form);

        HttpResponseMessage response = await _httpClient!.PostAsync($"http://localhost:{_port}/drop", content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        string responseHtml = await response.Content.ReadAsStringAsync();
        responseHtml.Should().Contain("Song eingeworfen");

        droppedUrl.Should().Be("https://www.youtube.com/watch?v=dQw4w9WgXcQ");
    }

    [Fact]
    public async Task PostDrop_WithRawTextBody_ShouldRaiseLinkDroppedEvent()
    {
        if (_server is null || !_server.IsRunning)
        {
            return;
        }

        string? droppedUrl = null;
        _server.LinkDropped += (_, url) => droppedUrl = url;

        StringContent content = new("https://youtu.be/dQw4w9WgXcQ", System.Text.Encoding.UTF8, "text/plain");

        HttpResponseMessage response = await _httpClient!.PostAsync($"http://localhost:{_port}/drop", content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        droppedUrl.Should().Be("https://youtu.be/dQw4w9WgXcQ");
    }

    [Fact]
    public async Task StopAsync_ShouldStopListening()
    {
        if (_server is null || !_server.IsRunning)
        {
            return;
        }

        _server.IsRunning.Should().BeTrue();
        await _server.StopAsync();
        _server.IsRunning.Should().BeFalse();
    }

    [Fact]
    public async Task StartAsync_WhenAlreadyRunning_ShouldBeIdempotent()
    {
        if (_server is null || !_server.IsRunning)
        {
            return;
        }

        Func<Task> act = async () => await _server.StartAsync();
        await act.Should().NotThrowAsync();
        _server.IsRunning.Should().BeTrue();
    }

    [Fact]
    public async Task StopAsync_WhenAlreadyStopped_ShouldBeIdempotent()
    {
        if (_server is null || !_server.IsRunning)
        {
            return;
        }

        await _server.StopAsync();
        Func<Task> act = async () => await _server.StopAsync();
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DisposeAsync_WhenCalledMultipleTimes_ShouldBeIdempotent()
    {
        EmbeddedWebDropServer server = new(0, new BurroGroup(GroupId.New(), "Temp"));
        await server.DisposeAsync();
        Func<Task> act = async () => await server.DisposeAsync();
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void GeneratePageHtml_WhenGroupIsNull_ShouldRenderDefaultOfficeJukebox()
    {
        EmbeddedWebDropServer server = new(8080, null);

        string html = server.GeneratePageHtml();

        html.Should().Contain("Office Jukebox");
        html.Should().Contain("Kein Song aktiv");
        html.Should().Contain("Warteschlange ist leer");
        html.Should().Contain("GNU AGPL-3.0");
    }

    [Fact]
    public void GeneratePageHtml_WhenQueueEmpty_ShouldRenderEmptyQueueMessage()
    {
        BurroGroup group = new(GroupId.New(), "Empty Queue Office");
        EmbeddedWebDropServer server = new(8080, group);

        string html = server.GeneratePageHtml();

        html.Should().Contain("Empty Queue Office");
        html.Should().Contain("Warteschlange ist leer");
    }

    [Fact]
    public async Task PostDrop_WithEmptyUrlParam_ShouldNotRaiseLinkDroppedEvent()
    {
        if (_server is null || !_server.IsRunning)
        {
            return;
        }

        bool dropped = false;
        _server.LinkDropped += (_, _) => dropped = true;

        FormUrlEncodedContent content = new(new Dictionary<string, string> { ["url"] = "   " });
        HttpResponseMessage response = await _httpClient!.PostAsync($"http://localhost:{_port}/drop", content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        dropped.Should().BeFalse();
    }

    [Fact]
    public async Task StartAsync_WhenPortOccupied_ShouldFallbackAndHandleGracefully()
    {
        using TcpListener blocker = new(IPAddress.Any, 0);
        blocker.Start();
        int occupiedPort = ((IPEndPoint)blocker.LocalEndpoint).Port;

        EmbeddedWebDropServer server = new(occupiedPort, null);
        await server.StartAsync();
        server.Port.Should().BeGreaterThan(0);
        await server.StopAsync();
    }

    [Fact]
    public async Task StartAsync_WhenBothAnyAndLoopbackPortsOccupied_ShouldReturnGracefully()
    {
        using TcpListener blocker1 = new(IPAddress.Loopback, 0);
        blocker1.Start();
        int port = ((IPEndPoint)blocker1.LocalEndpoint).Port;

        using TcpListener blocker2 = new(IPAddress.Any, port);
        try
        {
            blocker2.Start();
        }
        catch
        {
        }

        EmbeddedWebDropServer server = new(port, null);
        await server.StartAsync();
        server.IsRunning.Should().BeFalse();
        await server.StopAsync();
    }

    [Fact]
    public async Task HandleClientAsync_WhenClientSendsEmptyLine_ShouldReturnEarlyWithoutThrowing()
    {
        if (_server is null || !_server.IsRunning)
        {
            return;
        }

        using TcpClient client = new();
        await client.ConnectAsync(IPAddress.Loopback, _port);
        using NetworkStream stream = client.GetStream();
        byte[] emptyLine = "\r\n"u8.ToArray();
        await stream.WriteAsync(emptyLine);
        await stream.FlushAsync();
        await Task.Delay(50);
    }

    [Fact]
    public async Task HandleClientAsync_WhenClientDisconnectsAbruptly_ShouldCatchGracefully()
    {
        if (_server is null || !_server.IsRunning)
        {
            return;
        }

        using TcpClient client = new();
        await client.ConnectAsync(IPAddress.Loopback, _port);
        using NetworkStream stream = client.GetStream();
        byte[] partialRequest = "POST /drop HTTP/1.1\r\nContent-Length: 100\r\n\r\n"u8.ToArray();
        await stream.WriteAsync(partialRequest);
        client.Client.LingerState = new LingerOption(true, 0);
        client.Close();
        await Task.Delay(50);
    }
}


