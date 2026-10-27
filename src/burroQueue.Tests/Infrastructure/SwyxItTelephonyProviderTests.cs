using burroQueue.Domain.Model;
using burroQueue.Infrastructure.Telephony;
using FluentAssertions;
using Xunit;

namespace burroQueue.Tests.Infrastructure;

public sealed class SwyxItTelephonyProviderTests
{
    [Theory]
    [InlineData(3, TelephonyState.Ringing)]
    [InlineData(1, TelephonyState.Connecting)]
    [InlineData(2, TelephonyState.Connecting)]
    [InlineData(4, TelephonyState.Connecting)]
    [InlineData(5, TelephonyState.Connecting)]
    [InlineData(8, TelephonyState.ActiveCall)]
    [InlineData(10, TelephonyState.ActiveCall)]
    [InlineData(0, TelephonyState.Idle)]
    public void MapLineStateToTelephonyState_ShouldMapClMgrStateCorrectly(int clMgrState, TelephonyState expected)
    {
        TelephonyState mapped = SwyxItTelephonyProvider.MapLineStateToTelephonyState(clMgrState);

        mapped.Should().Be(expected);
    }
}

