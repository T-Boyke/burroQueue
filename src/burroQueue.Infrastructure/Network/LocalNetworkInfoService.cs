using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace burroQueue.Infrastructure.Network;

public sealed class LocalNetworkInfoService : ILocalNetworkInfoService
{
    public string GetPrimaryIPv4Address()
    {
        try
        {
            NetworkInterface[] interfaces = NetworkInterface.GetAllNetworkInterfaces();

            foreach (NetworkInterface nic in interfaces)
            {
                if (nic.OperationalStatus != OperationalStatus.Up ||
                    nic.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                    nic.NetworkInterfaceType == NetworkInterfaceType.Tunnel)
                {
                    continue;
                }

                IPInterfaceProperties properties = nic.GetIPProperties();
                if (properties.GatewayAddresses.Count == 0)
                {
                    continue;
                }

                foreach (UnicastIPAddressInformation unicast in properties.UnicastAddresses)
                {
                    if (unicast.Address.AddressFamily == AddressFamily.InterNetwork)
                    {
                        string ipString = unicast.Address.ToString();
                        if (!ipString.StartsWith("127.", StringComparison.Ordinal) &&
                            !ipString.StartsWith("169.254.", StringComparison.Ordinal))
                        {
                            return ipString;
                        }
                    }
                }
            }

            using UdpClient socket = new();
            socket.Connect("8.8.8.8", 65530);
            if (socket.Client.LocalEndPoint is IPEndPoint endPoint)
            {
                return endPoint.Address.ToString();
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
