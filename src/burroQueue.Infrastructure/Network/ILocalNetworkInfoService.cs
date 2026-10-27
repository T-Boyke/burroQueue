namespace burroQueue.Infrastructure.Network;

public interface ILocalNetworkInfoService
{
    string GetPrimaryIPv4Address();

    string GetWebDropUrl(int port);
}
