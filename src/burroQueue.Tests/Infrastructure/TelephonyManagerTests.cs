using burroQueue.Domain.Model;
using burroQueue.Infrastructure.Telephony;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace burroQueue.Tests.Infrastructure;

public sealed class TelephonyManagerTests : IAsyncLifetime, IDisposable
{
    private readonly ITelephonyProvider _provider1;
    private readonly ITelephonyProvider _provider2;
    private TelephonyManager? _manager;

    public TelephonyManagerTests()
    {
        _provider1 = Substitute.For<ITelephonyProvider>();
        _provider1.ProviderId.Returns("Provider1");

        _provider2 = Substitute.For<ITelephonyProvider>();
        _provider2.ProviderId.Returns("Provider2");
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (_manager is not null)
        {
            await _manager.DisposeAsync();
            _manager = null;
        }
    }

    public void Dispose()
    {
        DisposeAsync().GetAwaiter().GetResult();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Constructor_WithEmptyProviders_ShouldAddNullTelephonyProvider()
    {
        _manager = new TelephonyManager([]);

        _manager.AvailableProviders.Should().HaveCount(1);
        _manager.ActiveProvider.Should().BeOfType<NullTelephonyProvider>();
        _manager.CurrentState.Should().Be(TelephonyState.Idle);
    }

    [Fact]
    public async Task StartAsync_ShouldCallStartOnActiveProvider()
    {
        _manager = new TelephonyManager([_provider1, _provider2], defaultProviderId: "Provider1");

        await _manager.StartAsync();

        await _provider1.Received(1).StartAsync(Arg.Any<CancellationToken>());
        await _provider2.DidNotReceive().StartAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SwitchProviderAsync_ShouldStopOldAndStartNew()
    {
        _manager = new TelephonyManager([_provider1, _provider2], defaultProviderId: "Provider1");
        bool stateChangedFired = false;
        _manager.StateChanged += (_, e) =>
        {
            if (e.ProviderId == "Provider2" && e.State == TelephonyState.Idle)
            {
                stateChangedFired = true;
            }
        };

        await _manager.SwitchProviderAsync("Provider2");

        await _provider1.Received(1).StopAsync(Arg.Any<CancellationToken>());
        await _provider2.Received(1).StartAsync(Arg.Any<CancellationToken>());
        _manager.ActiveProvider.Should().Be(_provider2);
        stateChangedFired.Should().BeTrue();
    }

    [Fact]
    public async Task SwitchProviderAsync_WithUnknownOrSameProvider_ShouldDoNothing()
    {
        _manager = new TelephonyManager([_provider1, _provider2], defaultProviderId: "Provider1");

        await _manager.SwitchProviderAsync("UnknownProvider");
        await _manager.SwitchProviderAsync("Provider1");

        await _provider1.DidNotReceive().StopAsync(Arg.Any<CancellationToken>());
        _manager.ActiveProvider.Should().Be(_provider1);
    }

    [Fact]
    public void CallStateChanged_FromActiveProvider_ShouldUpdateCurrentStateAndNotify()
    {
        _manager = new TelephonyManager([_provider1, _provider2], defaultProviderId: "Provider1");
        TelephonyCallEventArgs? capturedEvent = null;
        _manager.StateChanged += (_, e) => capturedEvent = e;

        TelephonyCallEventArgs callEvent = new("Provider1", TelephonyState.ActiveCall);
        _provider1.CallStateChanged += Raise.Event<EventHandler<TelephonyCallEventArgs>>(_provider1, callEvent);

        _manager.CurrentState.Should().Be(TelephonyState.ActiveCall);
        capturedEvent.Should().NotBeNull();
        capturedEvent!.IsCallActive.Should().BeTrue();
    }

    [Fact]
    public void CallStateChanged_FromInactiveProvider_ShouldBeIgnored()
    {
        _manager = new TelephonyManager([_provider1, _provider2], defaultProviderId: "Provider1");
        bool stateChangedFired = false;
        _manager.StateChanged += (_, _) => stateChangedFired = true;

        TelephonyCallEventArgs callEvent = new("Provider2", TelephonyState.ActiveCall);
        _provider2.CallStateChanged += Raise.Event<EventHandler<TelephonyCallEventArgs>>(_provider2, callEvent);

        _manager.CurrentState.Should().Be(TelephonyState.Idle);
        stateChangedFired.Should().BeFalse();
    }
}
