using System.Net;
using burroQueue.Infrastructure.Network;
using FluentAssertions;
using Xunit;

namespace burroQueue.Tests.Infrastructure;

public sealed class LocalNetworkInfoServiceTests
{
    [Fact]
    public void GetPrimaryIPv4Address_ReturnsValidIPv4OrLocalhost()
    {
        // Arrange
        LocalNetworkInfoService service = new();

        // Act
        string ip = service.GetPrimaryIPv4Address();

        // Assert
        ip.Should().NotBeNullOrWhiteSpace();
        bool isValidIp = IPAddress.TryParse(ip, out IPAddress? parsed);
        isValidIp.Should().BeTrue();
        parsed!.AddressFamily.Should().Be(System.Net.Sockets.AddressFamily.InterNetwork);
    }

    [Fact]
    public void GetWebDropUrl_ReturnsFormattedHttpUrlWithPort()
    {
        // Arrange
        LocalNetworkInfoService service = new();

        // Act
        string url = service.GetWebDropUrl(8080);

        // Assert
        url.Should().StartWith("http://");
        url.Should().EndWith(":8080");
        Uri.TryCreate(url, UriKind.Absolute, out Uri? uri).Should().BeTrue();
        uri!.Port.Should().Be(8080);
    }
}
