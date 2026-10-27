using System.Globalization;
using System.Windows;
using burroQueue.Presentation.Converters;
using FluentAssertions;
using Xunit;

namespace burroQueue.Tests.Presentation;

public sealed class BooleanToVisibilityConverterTests
{
    private readonly BooleanToVisibilityConverter _converter = new();

    [Fact]
    public void Convert_WhenValueIsTrue_ShouldReturnVisible()
    {
        object result = _converter.Convert(true, typeof(Visibility), null, CultureInfo.InvariantCulture);

        result.Should().Be(Visibility.Visible);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(null)]
    [InlineData("not a bool")]
    public void Convert_WhenValueIsNotTrue_ShouldReturnCollapsed(object? input)
    {
        object result = _converter.Convert(input, typeof(Visibility), null, CultureInfo.InvariantCulture);

        result.Should().Be(Visibility.Collapsed);
    }

    [Fact]
    public void ConvertBack_WhenVisible_ShouldReturnTrue()
    {
        object result = _converter.ConvertBack(Visibility.Visible, typeof(bool), null, CultureInfo.InvariantCulture);

        result.Should().Be(true);
    }

    [Fact]
    public void ConvertBack_WhenCollapsed_ShouldReturnFalse()
    {
        object result = _converter.ConvertBack(Visibility.Collapsed, typeof(bool), null, CultureInfo.InvariantCulture);

        result.Should().Be(false);
    }
}
