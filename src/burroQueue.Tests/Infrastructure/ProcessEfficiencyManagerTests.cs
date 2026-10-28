using burroQueue.Infrastructure.Performance;
using FluentAssertions;
using Xunit;

namespace burroQueue.Tests.Infrastructure;

public sealed class ProcessEfficiencyManagerTests
{
    [Fact]
    public void EnableEfficiencyMode_ShouldNotThrow()
    {
        ProcessEfficiencyManager manager = new();

        Action act = () => manager.EnableEfficiencyMode();

        act.Should().NotThrow();
    }

    [Fact]
    public void TrimMemoryWorkingSet_ShouldNotThrow()
    {
        ProcessEfficiencyManager manager = new();

        Action act = () => manager.TrimMemoryWorkingSet();

        act.Should().NotThrow();
    }
}

