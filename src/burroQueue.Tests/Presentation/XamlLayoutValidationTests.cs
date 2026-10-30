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
    [InlineData("AboutWindow.xaml")]
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

    [Fact]
    public void TrackToastWindow_ShouldUseCompactWin11DimensionsSingleLineEllipsisAndCircularAvatarBadge()
    {
        string path = Path.Combine(XamlSymbolValidationTests.FindSolutionRoot(), "src", "burroQueue.Presentation", "Views", "TrackToastWindow.xaml");
        XDocument doc = XDocument.Load(path);
        XElement root = doc.Root!;
        XNamespace xNs = "http://schemas.microsoft.com/winfx/2006/xaml";

        root.Attribute("Width")?.Value.Should().Be("320");
        root.Attribute("Height")?.Value.Should().Be("68");
        root.Attribute("MinWidth")?.Value.Should().Be("0");
        root.Attribute("MinHeight")?.Value.Should().Be("0");
        root.Attribute("SizeToContent")?.Value.Should().Be("Manual");
        root.Attribute("Focusable")?.Value.Should().Be("False");
        root.Attribute("ShowActivated")?.Value.Should().Be("False");

        XElement? msgText = root.Descendants(_wpfNs + "TextBlock")
            .FirstOrDefault(e => e.Attribute(xNs + "Name")?.Value == "MessageText");
        msgText.Should().NotBeNull();
        msgText!.Attribute("TextWrapping")?.Value.Should().Be("NoWrap");
        msgText.Attribute("TextTrimming")?.Value.Should().Be("CharacterEllipsis");

        XElement? badge = root.Descendants(_wpfNs + "Border")
            .FirstOrDefault(e => e.Attribute(xNs + "Name")?.Value == "CollaboratorAvatarBadge");
        badge.Should().NotBeNull("TrackToastWindow must include the circular 20x20 CollaboratorAvatarBadge on the cover corner");
        badge!.Attribute("Width")?.Value.Should().Be("20");
        badge.Attribute("Height")?.Value.Should().Be("20");
        badge.Attribute("CornerRadius")?.Value.Should().Be("10");
    }

    [Fact]
    public void MainWindow_ShouldConsolidatePlayPauseButtonCollapseQueueContainerAndRenderCollaboratorAvatarEllipse()
    {
        string path = Path.Combine(XamlSymbolValidationTests.FindSolutionRoot(), "src", "burroQueue.Presentation", "Views", "MainWindow.xaml");
        string rawXaml = File.ReadAllText(path);

        rawXaml.Should().Contain("TogglePlayPauseCommand", "Play and Pause must be consolidated into a single stateful TogglePlayPauseCommand button (#12)");
        rawXaml.Should().NotContain("Command=\"{Binding PauseCommand}\"", "Separate Pause button must be removed (#12)");
        rawXaml.Should().Contain("IsQuickAddInputVisible", "Quick-Add row must bind visibility to IsQuickAddInputVisible (#11)");
        rawXaml.Should().Contain("QueueContainerBorder", "Outer queue Border must collapse/shrink cleanly without empty space (#8)");
        rawXaml.Should().Contain("AvatarUriToImageSourceConverter", "Queue and Now-Playing items must render the collaborator's circular YouTube avatar (#278)");
        rawXaml.Should().Contain("QueueViewItems", "Queue ListView must bind to non-destructive cursor QueueViewItems (#9)");
        rawXaml.Should().Contain("IsCurrentlyPlaying", "Active track row must highlight via IsCurrentlyPlaying (#9)");
        rawXaml.Should().Contain("ToggleShuffleCommand", "Transport bar must expose Shuffle toggle button (#10)");
        rawXaml.Should().Contain("ArrowShuffle24", "Shuffle button must use SymbolRegular.ArrowShuffle24 (#10)");
    }

    [Fact]
    public void SettingsWindow_ShouldIncludeAboutProjectAndStackCardWithYtDlpAuthorAndIssuesLinks()
    {
        string path = Path.Combine(XamlSymbolValidationTests.FindSolutionRoot(), "src", "burroQueue.Presentation", "Views", "SettingsWindow.xaml");
        string rawXaml = File.ReadAllText(path);

        rawXaml.Should().Contain("OpenYtDlpRepositoryCommand", "SettingsWindow About card must link to yt-dlp (#14)");
        rawXaml.Should().Contain("OpenAuthorProfileCommand", "SettingsWindow About card must link to @T-Boyke (#14)");
        rawXaml.Should().Contain("ReportBugOnGitHubCommand", "SettingsWindow About card must provide direct GitHub Bug report shortcut (#14)");
        rawXaml.Should().Contain("AuthorAvatarUri", "SettingsWindow About card must render the circular 36x36 @T-Boyke avatar (#14)");
    }
}
