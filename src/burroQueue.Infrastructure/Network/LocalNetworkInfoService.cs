using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace burroQueue.Infrastructure.Network;

public sealed class LocalNetworkInfoService : ILocalNetworkInfoService
{
    public string GetPrimaryIPv4Address()
    {
        return ResolvePrimaryIPv4Address();
    }

    internal static string ResolvePrimaryIPv4Address(
        Func<IEnumerable<(OperationalStatus Status, NetworkInterfaceType Type, bool HasGateway, IEnumerable<IPAddress> Addresses)>>? getInterfaces = null,
        Func<string?>? fallbackSocketResolver = null)
    {
        try
        {
            IEnumerable<(OperationalStatus Status, NetworkInterfaceType Type, bool HasGateway, IEnumerable<IPAddress> Addresses)> adapters;
            if (getInterfaces is not null)
            {
                adapters = getInterfaces();
            }
            else
            {
                adapters = NetworkInterface.GetAllNetworkInterfaces()
                    .Select(nic =>
                    {
                        IPInterfaceProperties? props = null;
                        try { props = nic.GetIPProperties(); } catch { }
                        bool hasGateway = props?.GatewayAddresses.Count > 0;
                        IEnumerable<IPAddress> addrs = props?.UnicastAddresses.Select(u => u.Address) ?? [];
                        return (nic.OperationalStatus, nic.NetworkInterfaceType, hasGateway, addrs);
                    });
            }

            foreach (var (status, type, hasGateway, addresses) in adapters)
            {
                if (status != OperationalStatus.Up ||
                    type == NetworkInterfaceType.Loopback ||
                    type == NetworkInterfaceType.Tunnel ||
                    !hasGateway)
                {
                    continue;
                }

                foreach (IPAddress address in addresses)
                {
                    if (address.AddressFamily == AddressFamily.InterNetwork)
                    {
                        string ipString = address.ToString();
                        if (!ipString.StartsWith("127.", StringComparison.Ordinal) &&
                            !ipString.StartsWith("169.254.", StringComparison.Ordinal))
                        {
                            return ipString;
                        }
                    }
                }
            }

            if (fallbackSocketResolver is not null)
            {
                string? fallback = fallbackSocketResolver();
                if (!string.IsNullOrEmpty(fallback))
                {
                    return fallback;
                }
            }
            else
            {
                using UdpClient socket = new();
                socket.Connect("8.8.8.8", 65530);
                if (socket.Client.LocalEndPoint is IPEndPoint endPoint)
                {
                    return endPoint.Address.ToString();
                }
            }
        }
        catch
        {
            // Bei Offline-Betrieb oder fehlenden Netzwerkadaptern
        }

        return "127.0.0.1";
    }

    public string GetWebDropUrl(int port)
    {
        string ip = GetPrimaryIPv4Address();
        return $"http://{ip}:{port}";
    }
}
