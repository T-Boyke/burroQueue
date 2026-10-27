using System.Globalization;
using System.Windows;
using burroQueue.Presentation.Converters;
using FluentAssertions;
using Xunit;

namespace burroQueue.Tests.Presentation;

public sealed class NullToVisibilityConverterTests
{
    private readonly NullToVisibilityConverter _converter = new();

    [Fact]
    public void Convert_WhenNull_ShouldReturnCollapsed()
    {
        object result = _converter.Convert(null, typeof(Visibility), null, CultureInfo.InvariantCulture);

        result.Should().Be(Visibility.Collapsed);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\r\n")]
    public void Convert_WhenEmptyOrWhitespaceString_ShouldReturnCollapsed(string input)
    {
        object result = _converter.Convert(input, typeof(Visibility), null, CultureInfo.InvariantCulture);

        result.Should().Be(Visibility.Collapsed);
    }

    [Theory]
    [InlineData("Valid text")]
    [InlineData("https://youtube.com")]
    public void Convert_WhenNonEmptyString_ShouldReturnVisible(string input)
    {
        object result = _converter.Convert(input, typeof(Visibility), null, CultureInfo.InvariantCulture);

        result.Should().Be(Visibility.Visible);
    }

    [Fact]
    public void Convert_WhenNonStringObject_ShouldReturnVisible()
    {
        object result = _converter.Convert(42, typeof(Visibility), null, CultureInfo.InvariantCulture);

        result.Should().Be(Visibility.Visible);
    }

    [Fact]
    public void ConvertBack_ShouldThrowNotSupportedException()
    {
        Action act = () => _converter.ConvertBack(Visibility.Visible, typeof(object), null, CultureInfo.InvariantCulture);

        act.Should().Throw<NotSupportedException>();
    }
}
