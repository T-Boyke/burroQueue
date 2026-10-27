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

    [Fact]
    public void SetAutostart_EnableThenDisable_ShouldUpdateRegistryAndCleanUp()
    {
        WindowsAutostartService service = new();
        try
        {
            bool enableResult = service.SetAutostart(true);
            enableResult.Should().BeTrue();
            service.IsAutostartEnabled.Should().BeTrue();
        }
        finally
        {
            service.SetAutostart(false);
            service.IsAutostartEnabled.Should().BeFalse();
        }
    }

    [Fact]
    public void EvaluateAutostart_WhenExceptionOccurs_ShouldReturnFalse()
    {
        bool result = WindowsAutostartService.EvaluateAutostart(
            () => throw new UnauthorizedAccessException("Registry access denied"));

        result.Should().BeFalse();
    }

    [Fact]
    public void ApplyAutostart_WhenKeyIsNull_ShouldReturnFalse()
    {
        bool result = WindowsAutostartService.ApplyAutostart(true, () => null, "C:\\app.exe");

        result.Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ApplyAutostart_WhenEnableAndExePathEmpty_ShouldReturnFalse(string? exePath)
    {
        using Microsoft.Win32.RegistryKey testKey = Microsoft.Win32.Registry.CurrentUser.CreateSubKey("Software\\burroQueueTestRun");
        try
        {
            bool result = WindowsAutostartService.ApplyAutostart(true, () => testKey, exePath);
            result.Should().BeFalse();
        }
        finally
        {
            Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree("Software\\burroQueueTestRun", false);
        }
    }

    [Fact]
    public void ApplyAutostart_WhenExceptionOccurs_ShouldReturnFalse()
    {
        bool result = WindowsAutostartService.ApplyAutostart(
            true,
            () => throw new UnauthorizedAccessException("Permission denied"),
            "C:\\app.exe");

        result.Should().BeFalse();
    }
}

