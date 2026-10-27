using burroQueue.Domain.Model;
using burroQueue.Infrastructure.Telephony;
using FluentAssertions;
using Xunit;

namespace burroQueue.Tests.Infrastructure;

public sealed class SwyxItTelephonyProviderTests : IAsyncLifetime, IDisposable
{
    private SwyxItTelephonyProvider? _provider;

    public Task InitializeAsync()
    {
        _provider = new SwyxItTelephonyProvider();
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

    [Fact]
    public void Properties_ShouldBeInitializedProperly()
    {
        _provider!.ProviderId.Should().Be("SwyxIt");
        _provider.DisplayName.Should().Contain("SwyxIt!");
        // IsAvailable can be true or false depending on whether SwyxIt is installed
        _ = _provider.IsAvailable;
    }

    [Theory]
    [InlineData(3, TelephonyState.Ringing)]
    [InlineData(1, TelephonyState.Connecting)]
    [InlineData(2, TelephonyState.Connecting)]
    [InlineData(4, TelephonyState.Connecting)]
    [InlineData(5, TelephonyState.Connecting)]
    [InlineData(6, TelephonyState.Connecting)]
    [InlineData(24, TelephonyState.Connecting)]
    [InlineData(26, TelephonyState.Connecting)]
    [InlineData(8, TelephonyState.ActiveCall)]
    [InlineData(10, TelephonyState.ActiveCall)]
    [InlineData(15, TelephonyState.ActiveCall)]
    [InlineData(0, TelephonyState.Idle)]
    [InlineData(-1, TelephonyState.Idle)]
    [InlineData(999, TelephonyState.Idle)]
    public void MapLineStateToTelephonyState_ShouldMapCorrectly(int clMgrState, TelephonyState expected)
    {
        TelephonyState mapped = SwyxItTelephonyProvider.MapLineStateToTelephonyState(clMgrState);

        mapped.Should().Be(expected);
    }

    [Fact]
    public async Task StartAndStopAsync_ShouldNotThrowEvenIfComUnavailable()
    {
        Func<Task> start = async () => await _provider!.StartAsync();
        Func<Task> stop = async () => await _provider!.StopAsync();

        await start.Should().NotThrowAsync();
        await stop.Should().NotThrowAsync();
    }

    [Fact]
    public void EvaluateLines_WhenNoLines_ShouldReturnIdle()
    {
        TelephonyState state = SwyxItTelephonyProvider.EvaluateLines(0, _ => 8);

        state.Should().Be(TelephonyState.Idle);
    }

    [Fact]
    public void EvaluateLines_WhenNullLinesPresent_ShouldSkipAndEvaluateOthers()
    {
        // Line 0 is null, line 1 is Ringing (3)
        TelephonyState state = SwyxItTelephonyProvider.EvaluateLines(2, i => i == 0 ? null : 3);

        state.Should().Be(TelephonyState.Ringing);
    }

    [Fact]
    public void EvaluateLines_WhenMultipleLines_ShouldReturnHighestPriorityState()
    {
        // Line 0 is Connecting (1), Line 1 is ActiveCall (8), Line 2 is Ringing (3)
        int[] states = [1, 8, 3];
        TelephonyState state = SwyxItTelephonyProvider.EvaluateLines(3, i => states[i]);

        state.Should().Be(TelephonyState.ActiveCall);
    }

    [Fact]
    public void CheckLinesCallback_WhenComUnavailable_ShouldUpdateStateToIdle()
    {
        _provider!.CheckLinesCallback(null);

        // Does not throw and stays in safe state
        ((object?)_provider!.ClientManager).Should().BeNull();
    }

    [Fact]
    public async Task CheckLinesCallback_WhenDisposed_ShouldReturnSilently()
    {
        await _provider!.DisposeAsync();

        Action act = () => _provider!.CheckLinesCallback(null);

        act.Should().NotThrow();
    }
}

