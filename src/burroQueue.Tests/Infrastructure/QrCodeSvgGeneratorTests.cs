using burroQueue.Infrastructure.Web;
using FluentAssertions;
using Xunit;

namespace burroQueue.Tests.Infrastructure;

public sealed class QrCodeSvgGeneratorTests
{
    [Fact]
    public void GenerateSvg_WhenEmptyContent_ShouldReturnFallbackSvg()
    {
        QrCodeSvgGenerator generator = new();

        string svg = generator.GenerateSvg(string.Empty);

        svg.Should().NotBeNullOrWhiteSpace();
        svg.Should().StartWith("<svg");
        svg.Should().EndWith("</svg>");
    }

    [Fact]
    public void GenerateSvg_WhenValidUrl_ShouldReturnValidSvgWithModules()
    {
        QrCodeSvgGenerator generator = new();
        string url = "http://192.168.1.50:8080";

        string svg = generator.GenerateSvg(url, moduleSize: 5);

        svg.Should().NotBeNullOrWhiteSpace();
        svg.Should().StartWith("<svg");
        svg.Should().EndWith("</svg>");
        svg.Should().Contain("viewBox=");
        svg.Should().Contain("<rect");
        svg.Should().Contain("fill=\"#000000\"");
    }

    [Fact]
    public void GenerateSvg_WhenCustomModuleSize_ShouldScaleCorrectly()
    {
        QrCodeSvgGenerator generator = new();
        string url = "http://localhost:8080";

        string svgSmall = generator.GenerateSvg(url, moduleSize: 2);
        string svgLarge = generator.GenerateSvg(url, moduleSize: 10);

        svgSmall.Should().Contain("width=\"");
        svgLarge.Should().Contain("width=\"");
        svgSmall.Length.Should().BeGreaterThan(0);
        svgLarge.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public void GenerateMatrix_WhenValidUrl_ShouldReturnSquareMatrix()
    {
        QrCodeSvgGenerator generator = new();
        bool[,] matrix = generator.GenerateMatrix("http://192.168.2.109:8080");

        matrix.Should().NotBeNull();
        int rows = matrix.GetLength(0);
        int cols = matrix.GetLength(1);
        rows.Should().Be(cols);
        rows.Should().BeGreaterThan(20);
    }
}
