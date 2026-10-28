using burroQueue.Presentation.Services;
using FluentAssertions;
using Xunit;

namespace burroQueue.Tests.Presentation;

public sealed class SingleInstanceManagerTests
{
    [Fact]
    public void TryAcquireSingleInstance_WhenFirstInstance_ShouldReturnTrue()
    {
        string uniqueId = Guid.NewGuid().ToString("N");
        using SingleInstanceManager manager = new(mutexName: $"Test_Mutex_{uniqueId}", eventName: $"Test_Event_{uniqueId}");

        bool acquired = manager.TryAcquireSingleInstance();

        acquired.Should().BeTrue();
    }

    [Fact]
    public void TryAcquireSingleInstance_WhenSecondInstance_ShouldReturnFalse()
    {
        string uniqueId = Guid.NewGuid().ToString("N");
        using SingleInstanceManager first = new(mutexName: $"Test_Mutex_{uniqueId}", eventName: $"Test_Event_{uniqueId}");
        using SingleInstanceManager second = new(mutexName: $"Test_Mutex_{uniqueId}", eventName: $"Test_Event_{uniqueId}");

        bool firstAcquired = first.TryAcquireSingleInstance();
        bool secondAcquired = second.TryAcquireSingleInstance();

        firstAcquired.Should().BeTrue();
        secondAcquired.Should().BeFalse();
    }

    [Fact]
    public void SignalExistingInstance_ShouldTriggerListenerCallback()
    {
        string uniqueId = Guid.NewGuid().ToString("N");
        using SingleInstanceManager primary = new(mutexName: $"Test_Mutex_{uniqueId}", eventName: $"Test_Event_{uniqueId}");
        using SingleInstanceManager secondary = new(mutexName: $"Test_Mutex_{uniqueId}", eventName: $"Test_Event_{uniqueId}");

        primary.TryAcquireSingleInstance();

        using ManualResetEventSlim signalReceived = new(false);
        primary.StartListeningForSecondaryInstance(() => signalReceived.Set());

        secondary.SignalExistingInstance();

        bool received = signalReceived.Wait(TimeSpan.FromSeconds(3));
        received.Should().BeTrue();
    }
}
