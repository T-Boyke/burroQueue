using System.Text;
using burroQueue.Infrastructure.Web;
using FluentAssertions;
using Xunit;

namespace burroQueue.Tests.Infrastructure;

public sealed class WebDropStylesheetProviderTests
{
    [Fact]
    public void GetCss_ShouldReturnCompiledUnoCssFluentStylesheetWithoutBootstrap()
    {
        string css = WebDropStylesheetProvider.GetCss();

        css.Should().NotBeNullOrWhiteSpace();
        css.Should().Contain("--bg-page");
        css.Should().Contain("--accent");
        css.Should().Contain("prefers-color-scheme");
        css.Should().Contain(".bq-shell");
        css.Should().Contain(".bq-btn-primary");
        css.Should().NotContainEquivalentOf("bootstrap");
    }

    [Fact]
    public void GetCssUtf8Bytes_ShouldMatchUtf8EncodingOfGetCssAndBeCached()
    {
        ReadOnlyMemory<byte> first = WebDropStylesheetProvider.GetCssUtf8Bytes();
        ReadOnlyMemory<byte> second = WebDropStylesheetProvider.GetCssUtf8Bytes();

        first.Length.Should().BeGreaterThan(500);
        Encoding.UTF8.GetString(first.Span).Should().Be(WebDropStylesheetProvider.GetCss());
        first.Equals(second).Should().BeTrue();
    }
}
