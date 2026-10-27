using burroQueue.Presentation.Services;
using FluentAssertions;
using Xunit;

namespace burroQueue.Tests.Presentation;

public sealed class SystrayNotificationServiceTests
{
    [Fact]
    public void ShowNotification_BeforeInitialize_ShouldNotThrow()
    {
        SystrayNotificationService service = new();

        Action act = () => service.ShowNotification("Title", "Message");

        act.Should().NotThrow();
    }

    [Fact]
    public void Dispose_WhenNotInitialized_ShouldBeIdempotentAndNotThrow()
    {
        SystrayNotificationService service = new();

        Action act = () =>
        {
            service.Dispose();
            service.Dispose();
        };

        act.Should().NotThrow();
    }
}
