using System.Net.Http;
using System.Net.Sockets;
using burroQueue.Domain.Model;
using burroQueue.Infrastructure.Telephony;
using FluentAssertions;
using Xunit;

namespace burroQueue.Tests.Infrastructure;

public sealed class StarfaceTelephonyProviderTests : IAsyncLifetime, IDisposable
{
    private StarfaceTelephonyProvider? _provider;
    private int _testPort;

    public Task InitializeAsync()
    {
        _testPort = GetFreeTcpPort();
        _provider = new StarfaceTelephonyProvider(_testPort);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        if (_provider is not null)
        {
            await _provider.DisposeAsync();
            _provider = null;
        }
    }

    public void Dispose()
    {
        DisposeAsync().GetAwaiter().GetResult();
        GC.SuppressFinalize(this);
    }

    private static int GetFreeTcpPort()
    {
        using TcpListener listener = new(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        int port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    [Fact]
    public void Properties_ShouldBeInitializedProperly()
    {
        _provider!.ProviderId.Should().Be("Starface");
        _provider.DisplayName.Should().Contain("STARFACE");
        _provider.IsAvailable.Should().Be(OperatingSystem.IsWindows());
    }

    [Theory]
    [InlineData("state=ringing", TelephonyState.Ringing)]
    [InlineData("state=RINGING", TelephonyState.Ringing)]
    [InlineData("klingeln", TelephonyState.Ringing)]
    [InlineData("incoming", TelephonyState.Ringing)]
    [InlineData("event=ringing", TelephonyState.Ringing)]
    [InlineData("state=connected", TelephonyState.ActiveCall)]
    [InlineData("active", TelephonyState.ActiveCall)]
    [InlineData("verbunden", TelephonyState.ActiveCall)]
    [InlineData("gespraech", TelephonyState.ActiveCall)]
    [InlineData("state=connecting", TelephonyState.Connecting)]
    [InlineData("waehlt", TelephonyState.Connecting)]
    [InlineData("dialing", TelephonyState.Connecting)]
    [InlineData("state=idle", TelephonyState.Idle)]
    [InlineData("ended", TelephonyState.Idle)]
    [InlineData("beendet", TelephonyState.Idle)]
    [InlineData("disconnected", TelephonyState.Idle)]
    [InlineData("unknown_value", TelephonyState.Idle)]
    [InlineData("", TelephonyState.Idle)]
    [InlineData(null, TelephonyState.Idle)]
    public void MapQueryToTelephonyState_ShouldMapCorrectly(string? query, TelephonyState expected)
    {
        TelephonyState mapped = StarfaceTelephonyProvider.MapQueryToTelephonyState(query);
        mapped.Should().Be(expected);
    }

    [Fact]
    public void TriggerStateManually_ShouldFireCallStateChangedEvent()
    {
        TelephonyCallEventArgs? captured = null;
        _provider!.CallStateChanged += (_, e) => captured = e;

        _provider.TriggerStateManually(TelephonyState.Ringing);

        captured.Should().NotBeNull();
        captured!.State.Should().Be(TelephonyState.Ringing);
        captured.IsCallActive.Should().BeTrue();
        captured.ProviderId.Should().Be("Starface");
    }

    [Fact]
    public async Task WebhookRequest_ShouldTriggerCallStateChangedAndRespondOk()
    {
        await _provider!.StartAsync();

        TelephonyCallEventArgs? captured = null;
        _provider.CallStateChanged += (_, e) => captured = e;

        using HttpClient client = new();
        string url = $"http://127.0.0.1:{_testPort}/starface/?state=connected";
        HttpResponseMessage response = await client.GetAsync(url);

        response.IsSuccessStatusCode.Should().BeTrue();
        string body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("ok");

        captured.Should().NotBeNull();
        captured!.State.Should().Be(TelephonyState.ActiveCall);
        captured.IsCallActive.Should().BeTrue();

        await _provider.StopAsync();
    }
}

