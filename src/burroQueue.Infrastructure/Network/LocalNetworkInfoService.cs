using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace burroQueue.Infrastructure.Network;

public sealed class LocalNetworkInfoService : ILocalNetworkInfoService
{
    private static readonly string[] _virtualAdapterKeywords =
    [
        "vethernet",
        "wsl",
        "hyper-v",
        "docker",
        "virtualbox",
        "vmware",
        "loopback",
        "tailscale",
        "npcap"
    ];

    public string GetPrimaryIPv4Address()
    {
        return ResolvePrimaryIPv4Address();
    }

    public IReadOnlyList<IPAddress> GetPhysicalIPv4Addresses()
    {
        return ResolvePhysicalIPv4Addresses();
    }

    internal static IReadOnlyList<IPAddress> ResolvePhysicalIPv4Addresses(
        Func<IEnumerable<(string Name, string Description, OperationalStatus Status, NetworkInterfaceType Type, IEnumerable<IPAddress> Addresses)>>? getInterfaces = null)
    {
        List<IPAddress> result = [];
        try
        {
            IEnumerable<(string Name, string Description, OperationalStatus Status, NetworkInterfaceType Type, IEnumerable<IPAddress> Addresses)> adapters =
                getInterfaces is not null
                    ? getInterfaces()
                    : NetworkInterface.GetAllNetworkInterfaces().Select(nic =>
                    {
                        IPInterfaceProperties? props = null;
                        try { props = nic.GetIPProperties(); } catch { }
                        IEnumerable<IPAddress> addrs = props?.UnicastAddresses.Select(u => u.Address) ?? [];
                        return (nic.Name ?? string.Empty, nic.Description ?? string.Empty, nic.OperationalStatus, nic.NetworkInterfaceType, addrs);
                    });

            foreach (var (name, desc, status, type, addresses) in adapters)
            {
                if (status != OperationalStatus.Up ||
                    type == NetworkInterfaceType.Loopback ||
                    type == NetworkInterfaceType.Tunnel ||
                    IsVirtualAdapter(name, desc))
                {
                    continue;
                }

                foreach (IPAddress address in addresses)
                {
                    if (address.AddressFamily == AddressFamily.InterNetwork && IsUsableLanIPv4(address.ToString()))
                    {
                        if (!result.Contains(address))
                        {
                            result.Add(address);
                        }
                    }
                }
            }
        }
        catch
        {
            // Offline fallback
        }

        return result;
    }

    private static bool IsVirtualAdapter(string name, string description)
    {
        foreach (string kw in _virtualAdapterKeywords)
        {
            if (name.Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                description.Contains(kw, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsUsableLanIPv4(string ipString) =>
        !ipString.StartsWith("127.", StringComparison.Ordinal) &&
        !ipString.StartsWith("169.254.", StringComparison.Ordinal) &&
        !ipString.StartsWith("172.17.", StringComparison.Ordinal) &&
        !ipString.StartsWith("172.18.", StringComparison.Ordinal) &&
        !ipString.StartsWith("172.19.", StringComparison.Ordinal) &&
        !ipString.StartsWith("172.2", StringComparison.Ordinal) &&
        !ipString.StartsWith("172.30.", StringComparison.Ordinal) &&
        !ipString.StartsWith("172.31.", StringComparison.Ordinal);

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
                            !ipString.StartsWith("169.254.", StringComparison.Ordinal) &&
                            !ipString.StartsWith("172.17.", StringComparison.Ordinal) &&
                            !ipString.StartsWith("172.18.", StringComparison.Ordinal) &&
                            !ipString.StartsWith("172.19.", StringComparison.Ordinal) &&
                            !ipString.StartsWith("172.2", StringComparison.Ordinal) &&
                            !ipString.StartsWith("172.30.", StringComparison.Ordinal) &&
                            !ipString.StartsWith("172.31.", StringComparison.Ordinal))
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
