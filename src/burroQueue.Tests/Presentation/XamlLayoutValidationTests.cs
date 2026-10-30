using System.IO;
using System.Xml.Linq;
using FluentAssertions;
using Xunit;

namespace burroQueue.Tests.Presentation;

public sealed class XamlLayoutValidationTests
{
    private static readonly XNamespace _wpfNs = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace _uiNs = "http://schemas.lepo.co/wpfui/2022/xaml";

    [Theory]
    [InlineData("MainWindow.xaml")]
    [InlineData("SettingsWindow.xaml")]
    public void FluentWindows_ShouldKeepRootGridMarginZero_SoTitleBarSpansFullWindowEdge(string fileName)
    {
        string path = Path.Combine(XamlSymbolValidationTests.FindSolutionRoot(), "src", "burroQueue.Presentation", "Views", fileName);
        XDocument doc = XDocument.Load(path);

        XElement rootGrid = doc.Root!.Element(_wpfNs + "Grid")!;
        rootGrid.Should().NotBeNull();

        string? rootMargin = rootGrid.Attribute("Margin")?.Value;
        (rootMargin is null or "0").Should().BeTrue($"{fileName} root Grid must not offset ui:TitleBar away from the top/right window edge");

        XElement? titleBar = rootGrid.Element(_uiNs + "TitleBar");
        titleBar.Should().NotBeNull($"{fileName} must place ui:TitleBar directly inside the zero-margin root Grid");
        (titleBar!.Attribute("Grid.Row")?.Value ?? "0").Should().Be("0");

        XElement? innerContentGrid = rootGrid.Elements(_wpfNs + "Grid")
            .FirstOrDefault(g => g.Attribute("Grid.Row")?.Value == "1");
        innerContentGrid.Should().NotBeNull($"{fileName} must wrap body content in an inner Grid at Grid.Row=1 with content margin");
        innerContentGrid!.Attribute("Margin")?.Value.Should().NotBeNullOrWhiteSpace();
    }
}
