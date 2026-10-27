using burroQueue.Infrastructure.Windows;
using FluentAssertions;
using Xunit;

namespace burroQueue.Tests.Infrastructure;

public sealed class WindowsAutostartServiceTests
{
    [Fact]
    public void IsAutostartEnabled_OnWindows_ShouldExecuteWithoutException()
    {
        WindowsAutostartService service = new();

        Action act = () => _ = service.IsAutostartEnabled;

        act.Should().NotThrow();
    }

    [Fact]
    public void SetAutostart_Disable_ShouldExecuteCleanly()
    {
        WindowsAutostartService service = new();

        bool result = service.SetAutostart(false);

        result.Should().BeTrue();
        service.IsAutostartEnabled.Should().BeFalse();
    }
}
