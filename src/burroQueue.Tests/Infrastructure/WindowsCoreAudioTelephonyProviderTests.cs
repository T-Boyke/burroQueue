using burroQueue.Domain.Model;
using burroQueue.Infrastructure.Telephony;
using FluentAssertions;
using Xunit;

namespace burroQueue.Tests.Infrastructure;

public sealed class WindowsCoreAudioTelephonyProviderTests
{
    [Fact]
    public async Task WindowsCoreAudioTelephonyProvider_LifecycleAndManualTrigger_ShouldFireEvents()
    {
        WindowsCoreAudioTelephonyProvider provider = new();

        provider.ProviderId.Should().Be("WindowsAudio");
        provider.DisplayName.Should().Contain("Windows Core Audio");
        _ = provider.IsAvailable;

        await provider.StartAsync();

        TelephonyCallEventArgs? received = null;
        provider.CallStateChanged += (_, e) => received = e;

        provider.TriggerStateManually(TelephonyState.ActiveCall);

        received.Should().NotBeNull();
        received!.State.Should().Be(TelephonyState.ActiveCall);
        received.IsCallActive.Should().BeTrue();

        await provider.StopAsync();
        await provider.DisposeAsync();
    }
}
