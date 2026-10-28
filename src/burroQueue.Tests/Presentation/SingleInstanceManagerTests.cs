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
        using SingleInstanceManager manager = new(mutexName: $@"Local\Test_Mutex_{uniqueId}", eventName: $@"Local\Test_Event_{uniqueId}");

        bool acquired = manager.TryAcquireSingleInstance();

        acquired.Should().BeTrue();
    }

    [Fact]
    public void TryAcquireSingleInstance_WhenSecondInstance_ShouldReturnFalse()
    {
        string uniqueId = Guid.NewGuid().ToString("N");
        using SingleInstanceManager first = new(mutexName: $@"Local\Test_Mutex_{uniqueId}", eventName: $@"Local\Test_Event_{uniqueId}");
        using SingleInstanceManager second = new(mutexName: $@"Local\Test_Mutex_{uniqueId}", eventName: $@"Local\Test_Event_{uniqueId}");

        bool firstAcquired = first.TryAcquireSingleInstance();
        bool secondAcquired = second.TryAcquireSingleInstance();

        firstAcquired.Should().BeTrue();
        secondAcquired.Should().BeFalse();
    }

    [Fact]
    public void SignalExistingInstance_ShouldTriggerListenerCallback()
    {
        string uniqueId = Guid.NewGuid().ToString("N");
        using SingleInstanceManager primary = new(mutexName: $@"Local\Test_Mutex_{uniqueId}", eventName: $@"Local\Test_Event_{uniqueId}");
        using SingleInstanceManager secondary = new(mutexName: $@"Local\Test_Mutex_{uniqueId}", eventName: $@"Local\Test_Event_{uniqueId}");

        primary.TryAcquireSingleInstance();

        using ManualResetEventSlim signalReceived = new(false);
        primary.StartListeningForSecondaryInstance(() => signalReceived.Set());

        Thread.Sleep(50);

        secondary.SignalExistingInstance();

        bool received = signalReceived.Wait(TimeSpan.FromSeconds(5));
        received.Should().BeTrue();
    }
}
