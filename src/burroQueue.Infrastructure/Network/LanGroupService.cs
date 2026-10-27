using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using burroQueue.Domain.Model;
using burroQueue.Infrastructure.Network.Messages;

namespace burroQueue.Infrastructure.Network;

public sealed class LanGroupService : ILanGroupService
{
    private const string MulticastAddressString = "239.255.42.99";
    private const int MulticastPort = 41234;

    private readonly IPAddress _multicastAddress = IPAddress.Parse(MulticastAddressString);
    private readonly IPEndPoint _multicastEndpoint;
    private UdpClient? _udpClient;
    private CancellationTokenSource? _cts;
    private Task? _listenerTask;
    private bool _isDisposed;

    public event EventHandler<DiscoveryBeaconMessage>? GroupDiscovered;

    public event EventHandler<SharedPlaylistSyncMessage>? CollaborativePlaylistReceived;

    public event EventHandler<GroupActionMessage>? GroupActionReceived;

    public event EventHandler<PlaybackCommandMessage>? PlaybackCommandReceived;

    public LanGroupService()
    {
        _multicastEndpoint = new IPEndPoint(_multicastAddress, MulticastPort);
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_udpClient is not null)
        {
            return Task.CompletedTask;
        }

        try
        {
            _udpClient = new UdpClient();
            _udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, MulticastPort));
            _udpClient.JoinMulticastGroup(_multicastAddress);
            _udpClient.EnableBroadcast = true;
        }
        catch (SocketException)
        {
            // Port blockiert oder Netzwerk nicht verfügbar
            return Task.CompletedTask;
        }

        _cts = new CancellationTokenSource();
        _listenerTask = Task.Run(() => ReceiveLoopAsync(_cts.Token), CancellationToken.None);

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_cts is not null)
        {
            await _cts.CancelAsync().ConfigureAwait(false);
        }

        if (_udpClient is not null)
        {
            try
            {
                _udpClient.DropMulticastGroup(_multicastAddress);
                _udpClient.Close();
            }
            catch
            {
                // Ignoriere Schließungsfehler
            }
            finally
            {
                _udpClient = null;
            }
        }

        if (_listenerTask is not null)
        {
            try
            {
                await _listenerTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Normal beim Stoppen
            }
        }
    }

    public async Task BroadcastBeaconAsync(BurroGroup group, ClientId hostId, string hostName, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(group);

        DiscoveryBeaconMessage beacon = new(
            group.Id.Value,
            group.Name,
            hostId.Value,
            hostName,
            group.Members.Count,
            group.CollaborativePlaylistLink is not null,
            group.CollaborativePlaylistLink?.ToString(),
            DateTimeOffset.UtcNow);

        await SendMessageAsync("BEACON", beacon, cancellationToken).ConfigureAwait(false);
    }

    public async Task ShareCollaborativePlaylistAsync(GroupId groupId, string playlistUrl, string sharedByName, CancellationToken cancellationToken = default)
    {
        SharedPlaylistSyncMessage message = new(
            groupId.Value,
            playlistUrl,
            sharedByName,
            DateTimeOffset.UtcNow);

        await SendMessageAsync("SHARE_PLAYLIST", message, cancellationToken).ConfigureAwait(false);
    }

    public async Task SendPlaybackCommandAsync(GroupId groupId, ClientId issuerId, string command, CancellationToken cancellationToken = default)
    {
        PlaybackCommandMessage message = new(
            groupId.Value,
            issuerId.Value,
            command,
            null,
            DateTimeOffset.UtcNow);

        await SendMessageAsync("PLAYBACK_CMD", message, cancellationToken).ConfigureAwait(false);
    }

    public async Task SendGroupActionAsync(GroupId groupId, ClientId actorId, string actionType, string? payload, CancellationToken cancellationToken = default)
    {
        GroupActionMessage message = new(
            groupId.Value,
            actorId.Value,
            actionType,
            payload,
            DateTimeOffset.UtcNow);

        await SendMessageAsync("GROUP_ACTION", message, cancellationToken).ConfigureAwait(false);
    }

    private async Task SendMessageAsync<T>(string type, T payload, CancellationToken cancellationToken)
    {
        if (_udpClient is null)
        {
            return;
        }

        try
        {
            string json = JsonSerializer.Serialize(new
            {
                Type = type,
                Data = payload
            });

            byte[] bytes = Encoding.UTF8.GetBytes(json);
            await _udpClient.SendAsync(bytes, bytes.Length, _multicastEndpoint).ConfigureAwait(false);
        }
        catch (SocketException)
        {
            // Netzwerk-Ausfall abfangen
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested && _udpClient is not null)
        {
            try
            {
                UdpReceiveResult result = await _udpClient.ReceiveAsync(token).ConfigureAwait(false);
                string json = Encoding.UTF8.GetString(result.Buffer);

                using JsonDocument doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("Type", out JsonElement typeElem) ||
                    !doc.RootElement.TryGetProperty("Data", out JsonElement dataElem))
                {
                    continue;
                }

                string? type = typeElem.GetString();
                string rawData = dataElem.GetRawText();

                switch (type)
                {
                    case "BEACON":
                        DiscoveryBeaconMessage? beacon = JsonSerializer.Deserialize<DiscoveryBeaconMessage>(rawData);
                        if (beacon is not null)
                        {
                            GroupDiscovered?.Invoke(this, beacon);
                        }
                        break;

                    case "SHARE_PLAYLIST":
                        SharedPlaylistSyncMessage? playlist = JsonSerializer.Deserialize<SharedPlaylistSyncMessage>(rawData);
                        if (playlist is not null)
                        {
                            CollaborativePlaylistReceived?.Invoke(this, playlist);
                        }
                        break;

                    case "PLAYBACK_CMD":
                        PlaybackCommandMessage? cmd = JsonSerializer.Deserialize<PlaybackCommandMessage>(rawData);
                        if (cmd is not null)
                        {
                            PlaybackCommandReceived?.Invoke(this, cmd);
                        }
                        break;

                    case "GROUP_ACTION":
                        GroupActionMessage? action = JsonSerializer.Deserialize<GroupActionMessage>(rawData);
                        if (action is not null)
                        {
                            GroupActionReceived?.Invoke(this, action);
                        }
                        break;
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                // Ignoriere unvollständige UDP-Pakete
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        await StopAsync().ConfigureAwait(false);
        _cts?.Dispose();
    }
}

