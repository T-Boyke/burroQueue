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

    [Fact]
    public void ResolvePrimaryIPv4Address_WithValidAdapter_ShouldReturnAdapterIp()
    {
        var adapters = new[]
        {
            (
                Status: System.Net.NetworkInformation.OperationalStatus.Up,
                Type: System.Net.NetworkInformation.NetworkInterfaceType.Ethernet,
                HasGateway: true,
                Addresses: (IEnumerable<IPAddress>)[IPAddress.Parse("192.168.1.105")]
            )
        };

        string ip = LocalNetworkInfoService.ResolvePrimaryIPv4Address(() => adapters);

        ip.Should().Be("192.168.1.105");
    }

    [Fact]
    public void ResolvePrimaryIPv4Address_WhenAdapterDownOrTunnelOrNoGateway_ShouldSkipAndFallback()
    {
        var adapters = new[]
        {
            (
                Status: System.Net.NetworkInformation.OperationalStatus.Down,
                Type: System.Net.NetworkInformation.NetworkInterfaceType.Ethernet,
                HasGateway: true,
                Addresses: (IEnumerable<IPAddress>)[IPAddress.Parse("192.168.1.50")]
            ),
            (
                Status: System.Net.NetworkInformation.OperationalStatus.Up,
                Type: System.Net.NetworkInformation.NetworkInterfaceType.Tunnel,
                HasGateway: true,
                Addresses: (IEnumerable<IPAddress>)[IPAddress.Parse("192.168.1.51")]
            ),
            (
                Status: System.Net.NetworkInformation.OperationalStatus.Up,
                Type: System.Net.NetworkInformation.NetworkInterfaceType.Ethernet,
                HasGateway: false,
                Addresses: (IEnumerable<IPAddress>)[IPAddress.Parse("192.168.1.52")]
            )
        };

        string ip = LocalNetworkInfoService.ResolvePrimaryIPv4Address(
            () => adapters,
            fallbackSocketResolver: () => "10.0.0.1");

        ip.Should().Be("10.0.0.1");
    }

    [Fact]
    public void ResolvePrimaryIPv4Address_WhenLoopbackOrApipaOrIpv6_ShouldSkipAndFallback()
    {
        var adapters = new[]
        {
            (
                Status: System.Net.NetworkInformation.OperationalStatus.Up,
                Type: System.Net.NetworkInformation.NetworkInterfaceType.Ethernet,
                HasGateway: true,
                Addresses: (IEnumerable<IPAddress>)[
                    IPAddress.Parse("127.0.0.1"),
                    IPAddress.Parse("169.254.5.5"),
                    IPAddress.Parse("::1"),
                    IPAddress.Parse("fe80::1")
                ]
            )
        };

        string ip = LocalNetworkInfoService.ResolvePrimaryIPv4Address(
            () => adapters,
            fallbackSocketResolver: () => "172.16.0.99");

        ip.Should().Be("172.16.0.99");
    }

    [Fact]
    public void ResolvePrimaryIPv4Address_WhenAllFailOrThrow_ShouldReturnDefaultLoopback()
    {
        string ip = LocalNetworkInfoService.ResolvePrimaryIPv4Address(
            () => throw new InvalidOperationException("Simulated adapter failure"),
            fallbackSocketResolver: () => throw new InvalidOperationException("Simulated socket failure"));

        ip.Should().Be("127.0.0.1");
    }

    [Fact]
    public void ResolvePrimaryIPv4Address_WhenNoAdaptersAndDefaultFallback_ShouldAttemptSocketOrReturnLoopback()
    {
        string ip = LocalNetworkInfoService.ResolvePrimaryIPv4Address(
            getInterfaces: () => []);

        ip.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void ResolvePrimaryIPv4Address_WhenNoAdaptersAndFallbackReturnsNull_ShouldFallbackToLoopback()
    {
        string ip = LocalNetworkInfoService.ResolvePrimaryIPv4Address(
            getInterfaces: () => [],
            fallbackSocketResolver: () => null);

        ip.Should().Be("127.0.0.1");
    }

    [Fact]
    public void ResolvePhysicalIPv4Addresses_ShouldExcludeVirtualHyperVWslDockerAndLoopbackAdapters()
    {
        var adapters = new[]
        {
            (
                Name: "vEthernet (WSL (Hyper-V firewall))",
                Description: "Hyper-V Virtual Ethernet Adapter",
                Status: System.Net.NetworkInformation.OperationalStatus.Up,
                Type: System.Net.NetworkInformation.NetworkInterfaceType.Ethernet,
                Addresses: (IEnumerable<IPAddress>)[IPAddress.Parse("172.24.80.1"), IPAddress.Parse("192.168.56.1")]
            ),
            (
                Name: "Ethernet",
                Description: "Intel(R) Ethernet Controller I226-V",
                Status: System.Net.NetworkInformation.OperationalStatus.Up,
                Type: System.Net.NetworkInformation.NetworkInterfaceType.Ethernet,
                Addresses: (IEnumerable<IPAddress>)[IPAddress.Parse("192.168.10.42"), IPAddress.Parse("169.254.1.1")]
            ),
            (
                Name: "Wi-Fi",
                Description: "Intel(R) Wi-Fi 7 BE200",
                Status: System.Net.NetworkInformation.OperationalStatus.Up,
                Type: System.Net.NetworkInformation.NetworkInterfaceType.Wireless80211,
                Addresses: (IEnumerable<IPAddress>)[IPAddress.Parse("10.10.20.15")]
            )
        };

        IReadOnlyList<IPAddress> physical = LocalNetworkInfoService.ResolvePhysicalIPv4Addresses(() => adapters);

        physical.Select(a => a.ToString()).Should().Equal("192.168.10.42", "10.10.20.15");
    }
}
