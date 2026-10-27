using burroQueue.Domain.Model;
using burroQueue.Infrastructure.Telephony;
using FluentAssertions;
using Xunit;

namespace burroQueue.Tests.Infrastructure;

public sealed class TelephonyManagerTests
{
    [Fact]
    public void Constructor_ShouldSelectDefaultProvider()
    {
        WindowsCoreAudioTelephonyProvider windowsProvider = new();
        NullTelephonyProvider nullProvider = new();

        TelephonyManager manager = new([windowsProvider, nullProvider], defaultProviderId: "WindowsAudio");

        manager.ActiveProvider.ProviderId.Should().Be("WindowsAudio");
        manager.CurrentState.Should().Be(TelephonyState.Idle);
    }

    [Fact]
    public async Task SwitchProviderAsync_ShouldChangeActiveProvider()
    {
        WindowsCoreAudioTelephonyProvider windowsProvider = new();
        NullTelephonyProvider nullProvider = new();

        TelephonyManager manager = new([windowsProvider, nullProvider], defaultProviderId: "WindowsAudio");

        await manager.SwitchProviderAsync("None");

        manager.ActiveProvider.ProviderId.Should().Be("None");
    }

    [Fact]
    public void ProviderStateChanged_WhenActiveProviderFires_ShouldForwardEvent()
    {
        WindowsCoreAudioTelephonyProvider windowsProvider = new();
        TelephonyManager manager = new([windowsProvider], defaultProviderId: "WindowsAudio");

        TelephonyCallEventArgs? received = null;
        manager.StateChanged += (_, e) => received = e;

        windowsProvider.TriggerStateManually(TelephonyState.Ringing);

        received.Should().NotBeNull();
        received!.State.Should().Be(TelephonyState.Ringing);
        received.IsCallActive.Should().BeTrue();
    }
}

