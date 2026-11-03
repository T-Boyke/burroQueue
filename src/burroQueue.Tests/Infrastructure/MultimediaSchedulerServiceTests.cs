using System.Runtime.InteropServices;
using burroQueue.Infrastructure.Performance;
using FluentAssertions;
using Xunit;

namespace burroQueue.Tests.Infrastructure;

public sealed class MultimediaSchedulerServiceTests
{
    [Fact]
    public void IsSupported_OnWindows_ShouldBeTrue()
    {
        MultimediaSchedulerService service = new();
        bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        service.IsSupported.Should().Be(isWindows);
    }

    [Fact]
    public void RegisterPlaybackThread_ShouldReturnDisposable_AndDisposeWithoutException()
    {
        MultimediaSchedulerService service = new();

        IDisposable scope = service.RegisterPlaybackThread();

        scope.Should().NotBeNull();
        scope.Dispose();
        scope.Dispose();
    }
}

