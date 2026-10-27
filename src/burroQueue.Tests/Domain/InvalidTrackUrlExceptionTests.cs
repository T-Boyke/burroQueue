using burroQueue.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace burroQueue.Tests.Domain;

public sealed class InvalidTrackUrlExceptionTests
{
    [Fact]
    public void Constructor_WithMessage_ShouldSetMessage()
    {
        InvalidTrackUrlException ex = new("Invalid URL format");

        ex.Message.Should().Be("Invalid URL format");
        ex.InnerException.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithMessageAndInnerException_ShouldSetBoth()
    {
        InvalidOperationException inner = new("Format failure");
        InvalidTrackUrlException ex = new("Parsing failed", inner);

        ex.Message.Should().Be("Parsing failed");
        ex.InnerException.Should().BeSameAs(inner);
    }
}
