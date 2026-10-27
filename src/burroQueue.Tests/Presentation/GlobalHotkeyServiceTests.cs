using burroQueue.Presentation.Services;
using FluentAssertions;
using Xunit;

namespace burroQueue.Tests.Presentation;

public sealed class GlobalHotkeyServiceTests
{
    [Fact]
    public void Dispose_WhenNotRegistered_ShouldBeSafeAndIdempotent()
    {
        GlobalHotkeyService service = new();

        Action act = () =>
        {
            service.Dispose();
            service.Dispose();
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void Register_WithNullWindow_ShouldThrowArgumentNullException()
    {
        GlobalHotkeyService service = new();

        Action act = () => service.Register(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
