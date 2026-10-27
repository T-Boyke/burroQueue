using burroQueue.Infrastructure.Telephony;
using FluentAssertions;
using Xunit;

namespace burroQueue.Tests.Infrastructure;

public sealed class NullTelephonyProviderTests
{
    [Fact]
    public async Task NullTelephonyProvider_PropertiesAndLifecycle_ShouldBeSafe()
    {
        NullTelephonyProvider provider = new();

        provider.ProviderId.Should().Be("None");
        provider.DisplayName.Should().Contain("Deaktiviert");
        provider.IsAvailable.Should().BeTrue();

        provider.CallStateChanged += (_, _) => { };
        provider.CallStateChanged -= (_, _) => { };

        await provider.StartAsync();
        await provider.StopAsync();
        await provider.DisposeAsync();
    }
}
