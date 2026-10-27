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
    public void DefaultDelegates_ShouldExecuteWithoutException()
    {
        WindowsAutostartService service = new();

        using Microsoft.Win32.RegistryKey? readKey = service.KeyOpener(false);

        using Microsoft.Win32.RegistryKey? writeKey = service.KeyOpener(true);
        writeKey.Should().NotBeNull();

        string? processPath = service.ProcessPathProvider();
        processPath.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void SetAutostart_Disable_ShouldExecuteCleanly()
    {
        const string testSubKey = @"Software\burroQueueTestAutostartDisable";
        WindowsAutostartService service = new()
        {
            KeyOpener = writable => writable
                ? Microsoft.Win32.Registry.CurrentUser.CreateSubKey(testSubKey)
                : Microsoft.Win32.Registry.CurrentUser.OpenSubKey(testSubKey, false),
            ProcessPathProvider = () => @"C:\Program Files\burroQueue\burroQueue.exe"
        };

        try
        {
            bool result = service.SetAutostart(false);

            result.Should().BeTrue();
            service.IsAutostartEnabled.Should().BeFalse();
        }
        finally
        {
            Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(testSubKey, false);
        }
    }

    [Fact]
    public void SetAutostart_EnableThenDisable_ShouldUpdateRegistryAndCleanUp()
    {
        const string testSubKey = @"Software\burroQueueTestAutostartEnable";
        WindowsAutostartService service = new()
        {
            KeyOpener = writable => writable
                ? Microsoft.Win32.Registry.CurrentUser.CreateSubKey(testSubKey)
                : Microsoft.Win32.Registry.CurrentUser.OpenSubKey(testSubKey, false),
            ProcessPathProvider = () => @"C:\Program Files\burroQueue\burroQueue.exe"
        };

        try
        {
            bool enableResult = service.SetAutostart(true);
            enableResult.Should().BeTrue();
            service.IsAutostartEnabled.Should().BeTrue();

            bool disableResult = service.SetAutostart(false);
            disableResult.Should().BeTrue();
            service.IsAutostartEnabled.Should().BeFalse();
        }
        finally
        {
            Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(testSubKey, false);
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
    public void EvaluateAutostart_WhenKeyIsNull_ShouldReturnFalse()
    {
        bool result = WindowsAutostartService.EvaluateAutostart(() => null);

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

