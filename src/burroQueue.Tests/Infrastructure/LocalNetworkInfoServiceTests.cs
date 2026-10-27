using System.Net;
using burroQueue.Infrastructure.Network;
using FluentAssertions;
using Xunit;

namespace burroQueue.Tests.Infrastructure;

public sealed class LocalNetworkInfoServiceTests
{
    private readonly LocalNetworkInfoService _service = new();

    [Fact]
    public void GetPrimaryIPv4Address_ShouldReturnValidIPv4Address()
    {
        string ip = _service.GetPrimaryIPv4Address();

        ip.Should().NotBeNullOrWhiteSpace();
        bool isParsed = IPAddress.TryParse(ip, out IPAddress? parsed);
        isParsed.Should().BeTrue();
        parsed!.AddressFamily.Should().Be(System.Net.Sockets.AddressFamily.InterNetwork);
    }

    [Fact]
    public void GetWebDropUrl_ShouldFormatValidHttpUrlWithPort()
    {
        string url = _service.GetWebDropUrl(8080);

        url.Should().StartWith("http://");
        url.Should().EndWith(":8080");
        Uri.TryCreate(url, UriKind.Absolute, out Uri? parsedUri).Should().BeTrue();
        parsedUri!.Port.Should().Be(8080);
    }
}
