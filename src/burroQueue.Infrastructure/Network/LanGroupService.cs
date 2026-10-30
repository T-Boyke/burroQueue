using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using burroQueue.Domain.Model;
using burroQueue.Infrastructure.Network.Messages;

namespace burroQueue.Infrastructure.Network;

public sealed class LanGroupService : ILanGroupService
{
    private const string _multicastAddressString = "239.255.42.99";
    private const int _defaultMulticastPort = 41234;

    private readonly IPAddress _multicastAddress = IPAddress.Parse(_multicastAddressString);
    private readonly ILocalNetworkInfoService _networkInfo;
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly List<IPAddress> _boundPhysicalInterfaces = [];
    private Func<DiscoveryBeaconMessage?>? _beaconResponder;
    private UdpClient? _udpClient;
    private CancellationTokenSource? _cts;
    private Task? _listenerTask;
    private bool _isDisposed;

    internal int Port { get; init; } = _defaultMulticastPort;

    private IPEndPoint MulticastEndpoint => new(_multicastAddress, Port);

    private IPEndPoint BroadcastEndpoint => new(IPAddress.Broadcast, Port);

    public event EventHandler<DiscoveryBeaconMessage>? GroupDiscovered;

    public event EventHandler<SharedPlaylistSyncMessage>? CollaborativePlaylistReceived;

    public event EventHandler<GroupActionMessage>? GroupActionReceived;

    public event EventHandler<PlaybackCommandMessage>? PlaybackCommandReceived;

    public event EventHandler<QueueSyncMessage>? QueueSyncReceived;

    public event EventHandler<SkipVoteMessage>? SkipVoteReceived;

    public event EventHandler<PeerTelephonyStateMessage>? TelephonyStateReceived;

    public event EventHandler<QueueOrderSyncMessage>? QueueOrderSyncReceived;

    public event EventHandler<TrackUpvoteMessage>? TrackUpvoteReceived;

    public event EventHandler<HostHandoffMessage>? HostHandoffReceived;

    public LanGroupService(ILocalNetworkInfoService? networkInfo = null)
    {
        _networkInfo = networkInfo ?? new LocalNetworkInfoService();
    }

    internal Func<UdpClient> UdpClientFactory { get; set; } = () => new UdpClient();

    internal UdpClient? UdpClient => _udpClient;

    public void SetBeaconResponder(Func<DiscoveryBeaconMessage?>? responder)
    {
        _beaconResponder = responder;
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_udpClient is not null)
        {
            return Task.CompletedTask;
        }

        try
        {
            _udpClient = UdpClientFactory();
            _udpClient.ExclusiveAddressUse = false;
            _udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, Port));
            _udpClient.MulticastLoopback = true;
            _udpClient.EnableBroadcast = true;
            _udpClient.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 32);

            _boundPhysicalInterfaces.Clear();
            IReadOnlyList<IPAddress> physicalIps = _networkInfo.GetPhysicalIPv4Addresses();
            foreach (IPAddress localIp in physicalIps)
            {
                try
                {
                    _udpClient.JoinMulticastGroup(_multicastAddress, localIp);
                    _boundPhysicalInterfaces.Add(localIp);
                }
                catch (SocketException)
                {
                    // Skip individual adapter failure
                }
            }

            if (_boundPhysicalInterfaces.Count > 0)
            {
                try
                {
                    _udpClient.Client.SetSocketOption(
                        SocketOptionLevel.IP,
                        SocketOptionName.MulticastInterface,
                        _boundPhysicalInterfaces[0].GetAddressBytes());
                }
                catch (SocketException)
                {
                }
            }
            else
            {
                _udpClient.JoinMulticastGroup(_multicastAddress);
            }
        }
        catch (SocketException)
        {
            _udpClient?.Dispose();
            _udpClient = null;
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
                if (_boundPhysicalInterfaces.Count > 0)
                {
                    foreach (IPAddress localIp in _boundPhysicalInterfaces)
                    {
                        try { _udpClient.DropMulticastGroup(_multicastAddress); } catch { }
                    }
                }
                else
                {
                    _udpClient.DropMulticastGroup(_multicastAddress);
                }

                _udpClient.Close();
            }
            catch
            {
                // Ignoriere Schließungsfehler
            }
            finally
            {
                _boundPhysicalInterfaces.Clear();
                _udpClient = null;
            }
        }

        if (_listenerTask is not null)
        {
            await _listenerTask.ConfigureAwait(false);
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

        await SendMessageAsync("BEACON", beacon, includeBroadcastFallback: true, cancellationToken).ConfigureAwait(false);
    }

    public async Task SendDiscoveryProbeAsync(ClientId requesterId, string requesterDisplayName, CancellationToken cancellationToken = default)
    {
        DiscoveryProbeMessage probe = new(
            requesterId.Value,
            string.IsNullOrWhiteSpace(requesterDisplayName) ? Environment.UserName : requesterDisplayName.Trim(),
            DateTimeOffset.UtcNow);

        await SendMessageAsync("DISCOVER_PROBE", probe, includeBroadcastFallback: true, cancellationToken).ConfigureAwait(false);
    }

    public async Task SendTelephonyStateAsync(
        GroupId groupId,
        ClientId clientId,
        string clientDisplayName,
        string providerId,
        bool isCallActive,
        CancellationToken cancellationToken = default)
    {
        PeerTelephonyStateMessage msg = new(
            groupId.Value,
            clientId.Value,
            string.IsNullOrWhiteSpace(clientDisplayName) ? Environment.UserName : clientDisplayName.Trim(),
            string.IsNullOrWhiteSpace(providerId) ? "VoIP" : providerId.Trim(),
            isCallActive,
            DateTimeOffset.UtcNow);

        await SendMessageAsync("PEER_TELEPHONY", msg, includeBroadcastFallback: false, cancellationToken).ConfigureAwait(false);
    }

    public async Task ShareCollaborativePlaylistAsync(GroupId groupId, string playlistUrl, string sharedByName, CancellationToken cancellationToken = default)
    {
        SharedPlaylistSyncMessage message = new(
            groupId.Value,
            playlistUrl,
            sharedByName,
            DateTimeOffset.UtcNow);

        await SendMessageAsync("SHARE_PLAYLIST", message, includeBroadcastFallback: false, cancellationToken).ConfigureAwait(false);
    }

    public async Task SendPlaybackCommandAsync(GroupId groupId, ClientId issuerId, string command, CancellationToken cancellationToken = default)
    {
        PlaybackCommandMessage message = new(
            groupId.Value,
            issuerId.Value,
            command,
            null,
            DateTimeOffset.UtcNow);

        await SendMessageAsync("PLAYBACK_CMD", message, includeBroadcastFallback: false, cancellationToken).ConfigureAwait(false);
    }

    public async Task SendGroupActionAsync(GroupId groupId, ClientId actorId, string actionType, string? payload, CancellationToken cancellationToken = default)
    {
        GroupActionMessage message = new(
            groupId.Value,
            actorId.Value,
            actionType,
            payload,
            DateTimeOffset.UtcNow);

        await SendMessageAsync("GROUP_ACTION", message, includeBroadcastFallback: false, cancellationToken).ConfigureAwait(false);
    }

    public async Task SendQueueSyncAsync(GroupId groupId, string action, Track track, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(track);

        QueueSyncMessage message = new(
            groupId.Value,
            action,
            track.VideoId,
            track.Url.ToString(),
            track.Title,
            track.SubmittedBy,
            DateTimeOffset.UtcNow,
            ArtistName: track.ArtistName,
            AddedByAvatarUrl: track.SubmitterAvatarUrl?.ToString());

        await SendMessageAsync("QUEUE_SYNC", message, includeBroadcastFallback: false, cancellationToken).ConfigureAwait(false);
    }

    public async Task SendSkipVoteAsync(GroupId groupId, string trackId, ClientId voterId, CancellationToken cancellationToken = default)
    {
        SkipVoteMessage message = new(
            groupId.Value,
            trackId,
            voterId.Value,
            DateTimeOffset.UtcNow);

        await SendMessageAsync("SKIP_VOTE", message, includeBroadcastFallback: false, cancellationToken).ConfigureAwait(false);
    }

    public async Task SendQueueOrderSyncAsync(
        GroupId groupId,
        ClientId issuerId,
        bool isShuffleEnabled,
        IReadOnlyList<string> orderedVideoIds,
        CancellationToken cancellationToken = default)
    {
        QueueOrderSyncMessage message = new(
            groupId.Value,
            issuerId.Value,
            isShuffleEnabled,
            orderedVideoIds ?? [],
            DateTimeOffset.UtcNow);

        await SendMessageAsync("QUEUE_ORDER_SYNC", message, includeBroadcastFallback: false, cancellationToken).ConfigureAwait(false);
    }

    public async Task SendTrackUpvoteAsync(
        GroupId groupId,
        string videoId,
        string trackUrl,
        string title,
        string voterUsername,
        bool isUpvoted,
        CancellationToken cancellationToken = default)
    {
        TrackUpvoteMessage message = new(
            groupId.Value,
            videoId,
            trackUrl,
            title,
            string.IsNullOrWhiteSpace(voterUsername) ? Environment.UserName : voterUsername.Trim(),
            isUpvoted,
            DateTimeOffset.UtcNow);

        await SendMessageAsync("TRACK_UPVOTE", message, includeBroadcastFallback: false, cancellationToken).ConfigureAwait(false);
    }

    public async Task SendHostHandoffAsync(
        GroupId groupId,
        ClientId previousHostId,
        QueuePlaybackCursor cursor,
        CancellationToken cancellationToken = default)
    {
        HostHandoffMessage message = new(
            groupId.Value,
            previousHostId.Value,
            cursor.ActiveVideoId,
            cursor.QueueIndex,
            cursor.PlaybackPosition.TotalSeconds,
            cursor.WasPlaying,
            DateTimeOffset.UtcNow);

        await SendMessageAsync("HOST_HANDOFF", message, includeBroadcastFallback: true, cancellationToken).ConfigureAwait(false);
    }

    private async Task SendMessageAsync<T>(string type, T payload, bool includeBroadcastFallback, CancellationToken cancellationToken)
    {
        UdpClient? client = _udpClient;
        if (client is null)
        {
            return;
        }

        string json = JsonSerializer.Serialize(new
        {
            Type = type,
            Data = payload
        });
        byte[] bytes = Encoding.UTF8.GetBytes(json);

        await _sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            IPEndPoint multicastEp = MulticastEndpoint;
            if (_boundPhysicalInterfaces.Count > 0)
            {
                foreach (IPAddress localIp in _boundPhysicalInterfaces)
                {
                    try
                    {
                        client.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastInterface, localIp.GetAddressBytes());
                    }
                    catch (SocketException)
                    {
                    }

                    await client.SendAsync(bytes, bytes.Length, multicastEp).ConfigureAwait(false);
                }
            }
            else
            {
                await client.SendAsync(bytes, bytes.Length, multicastEp).ConfigureAwait(false);
            }

            if (includeBroadcastFallback)
            {
                try
                {
                    await client.SendAsync(bytes, bytes.Length, BroadcastEndpoint).ConfigureAwait(false);
                }
                catch (SocketException)
                {
                    // Subnet broadcast may be restricted on certain networks
                }
            }
        }
        catch (Exception ex) when (ex is SocketException or ObjectDisposedException)
        {
            // Netzwerk-Ausfall abfangen
        }
        finally
        {
            _sendLock.Release();
        }
    }

    private async Task SendDirectUnicastAsync<T>(string type, T payload, IPEndPoint destination)
    {
        UdpClient? client = _udpClient;
        if (client is null)
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
            await client.SendAsync(bytes, bytes.Length, destination).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is SocketException or ObjectDisposedException)
        {
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

                    case "DISCOVER_PROBE":
                        DiscoveryBeaconMessage? responseBeacon = _beaconResponder?.Invoke();
                        if (responseBeacon is not null)
                        {
                            await SendDirectUnicastAsync("BEACON", responseBeacon, result.RemoteEndPoint).ConfigureAwait(false);
                            await SendMessageAsync("BEACON", responseBeacon, includeBroadcastFallback: false, token).ConfigureAwait(false);
                        }
                        break;

                    case "PEER_TELEPHONY":
                        PeerTelephonyStateMessage? peerCall = JsonSerializer.Deserialize<PeerTelephonyStateMessage>(rawData);
                        if (peerCall is not null)
                        {
                            TelephonyStateReceived?.Invoke(this, peerCall);
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

                    case "QUEUE_SYNC":
                        QueueSyncMessage? queueSync = JsonSerializer.Deserialize<QueueSyncMessage>(rawData);
                        if (queueSync is not null)
                        {
                            QueueSyncReceived?.Invoke(this, queueSync);
                        }
                        break;

                    case "SKIP_VOTE":
                        SkipVoteMessage? skipVote = JsonSerializer.Deserialize<SkipVoteMessage>(rawData);
                        if (skipVote is not null)
                        {
                            SkipVoteReceived?.Invoke(this, skipVote);
                        }
                        break;

                    case "QUEUE_ORDER_SYNC":
                        QueueOrderSyncMessage? orderSync = JsonSerializer.Deserialize<QueueOrderSyncMessage>(rawData);
                        if (orderSync is not null)
                        {
                            QueueOrderSyncReceived?.Invoke(this, orderSync);
                        }
                        break;

                    case "TRACK_UPVOTE":
                        TrackUpvoteMessage? upvoteMsg = JsonSerializer.Deserialize<TrackUpvoteMessage>(rawData);
                        if (upvoteMsg is not null)
                        {
                            TrackUpvoteReceived?.Invoke(this, upvoteMsg);
                        }
                        break;

                    case "HOST_HANDOFF":
                        HostHandoffMessage? handoff = JsonSerializer.Deserialize<HostHandoffMessage>(rawData);
                        if (handoff is not null)
                        {
                            HostHandoffReceived?.Invoke(this, handoff);
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
        _sendLock.Dispose();
    }
}
