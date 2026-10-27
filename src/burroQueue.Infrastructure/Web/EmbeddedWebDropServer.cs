using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace burroQueue.Infrastructure.Web;

public sealed class EmbeddedWebDropServer : IEmbeddedWebDropServer
{
    private TcpListener? _tcpListener;
    private readonly Domain.Model.BurroGroup? _group;
    private CancellationTokenSource? _cts;
    private Task? _listenTask;
    private bool _isRunning;
    private bool _isDisposed;
    private int _actualPort;

    public int Port => _actualPort > 0 ? _actualPort : _configuredPort;

    private readonly int _configuredPort;

    public bool IsRunning => _isRunning && _tcpListener is not null;

    public event EventHandler<string>? LinkDropped;

    public EmbeddedWebDropServer(int port = 8080, Domain.Model.BurroGroup? group = null)
    {
        _configuredPort = port;
        _actualPort = port;
        _group = group;
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (IsRunning)
        {
            return Task.CompletedTask;
        }

        try
        {
            TcpListener listener = new(IPAddress.Any, _configuredPort);
            listener.Start();
            _tcpListener = listener;
            if (listener.LocalEndpoint is IPEndPoint ep)
            {
                _actualPort = ep.Port;
            }
        }
        catch
        {
            try
            {
                TcpListener fallback = new(IPAddress.Loopback, _configuredPort);
                fallback.Start();
                _tcpListener = fallback;
                if (fallback.LocalEndpoint is IPEndPoint ep)
                {
                    _actualPort = ep.Port;
                }
            }
            catch
            {
                return Task.CompletedTask;
            }
        }

        _isRunning = true;
        _cts = new CancellationTokenSource();
        _listenTask = Task.Run(() => AcceptLoopAsync(_cts.Token), CancellationToken.None);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        _isRunning = false;

        if (_cts is not null)
        {
            await _cts.CancelAsync().ConfigureAwait(false);
        }

        if (_tcpListener is not null)
        {
            _tcpListener.Stop();
            _tcpListener = null;
        }

        if (_listenTask is not null)
        {
            await _listenTask.ConfigureAwait(false);
            _listenTask = null;
        }
    }

    private async Task AcceptLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested && _isRunning && _tcpListener is not null)
        {
            try
            {
                TcpClient client = await _tcpListener.AcceptTcpClientAsync(token).ConfigureAwait(false);
                _ = Task.Run(() => HandleClientAsync(client, token), CancellationToken.None);
            }
            catch (Exception ex) when (ex is OperationCanceledException or SocketException or ObjectDisposedException or InvalidOperationException)
            {
                break;
            }
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken token)
    {
        using (client)
        {
            try
            {
                client.ReceiveTimeout = 5000;
                client.SendTimeout = 5000;

                using NetworkStream stream = client.GetStream();
                using StreamReader reader = new(stream, new UTF8Encoding(false), leaveOpen: true);

                string? requestLine = await reader.ReadLineAsync(token).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(requestLine))
                {
                    return;
                }

                string[] requestParts = requestLine.Split(' ');
                string method = requestParts.Length > 0 ? requestParts[0].ToUpperInvariant() : "GET";
                string path = requestParts.Length > 1 ? requestParts[1] : "/";

                int contentLength = 0;
                string? contentType = null;

                string? headerLine;
                while (!string.IsNullOrEmpty(headerLine = await reader.ReadLineAsync(token).ConfigureAwait(false)))
                {
                    int colonIndex = headerLine.IndexOf(':');
                    if (colonIndex > 0)
                    {
                        string headerName = headerLine[..colonIndex].Trim();
                        string headerValue = headerLine[(colonIndex + 1)..].Trim();

                        if (headerName.Equals("Content-Length", StringComparison.OrdinalIgnoreCase) &&
                            int.TryParse(headerValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out int cl))
                        {
                            contentLength = cl;
                        }
                        else if (headerName.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
                        {
                            contentType = headerValue;
                        }
                    }
                }

                if (method == "POST" && (path == "/drop" || path.StartsWith("/drop?", StringComparison.Ordinal)))
                {
                    string body = string.Empty;
                    if (contentLength > 0)
                    {
                        char[] buffer = new char[contentLength];
                        int readTotal = 0;
                        while (readTotal < contentLength)
                        {
                            int read = await reader.ReadBlockAsync(buffer, readTotal, contentLength - readTotal).ConfigureAwait(false);
                            if (read <= 0)
                            {
                                break;
                            }
                            readTotal += read;
                        }
                        body = new string(buffer, 0, readTotal);
                    }

                    string submittedUrl = body;
                    if (contentType?.Contains("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        string[] pairs = body.Split('&');
                        foreach (string pair in pairs)
                        {
                            string[] kvp = pair.Split('=', 2);
                            if (kvp.Length == 2 && kvp[0] == "url")
                            {
                                submittedUrl = WebUtility.UrlDecode(kvp[1]);
                                break;
                            }
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(submittedUrl))
                    {
                        LinkDropped?.Invoke(this, submittedUrl.Trim());
                    }

                    string responseHtml = "<!DOCTYPE html><html lang='de'><body style='font-family:sans-serif;text-align:center;padding:40px;background:#121212;color:#fff;'><h2>Song eingeworfen!</h2><p>Dein Link l&auml;uft gleich auf der Jukebox.</p><a href='/' style='color:#4fc3f7;'>Weiteren Link einwerfen</a></body></html>";
                    byte[] responseBytes = Encoding.UTF8.GetBytes(responseHtml);
                    byte[] headerBytes = Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: {responseBytes.Length}\r\nConnection: close\r\n\r\n");

                    await stream.WriteAsync(headerBytes, token).ConfigureAwait(false);
                    await stream.WriteAsync(responseBytes, token).ConfigureAwait(false);
                    await stream.FlushAsync(token).ConfigureAwait(false);
                }
                else
                {
                    string pageHtml = GeneratePageHtml();
                    byte[] pageBytes = Encoding.UTF8.GetBytes(pageHtml);
                    byte[] headerBytes = Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: {pageBytes.Length}\r\nConnection: close\r\n\r\n");

                    await stream.WriteAsync(headerBytes, token).ConfigureAwait(false);
                    await stream.WriteAsync(pageBytes, token).ConfigureAwait(false);
                    await stream.FlushAsync(token).ConfigureAwait(false);
                }
            }
            catch (Exception ex) when (ex is IOException or SocketException or OperationCanceledException)
            {
                // Verbindungsabbrüche durch Client ignorieren
            }
        }
    }

    public string GeneratePageHtml()
    {
        StringBuilder sb = new();
        sb.Append(@"<!DOCTYPE html>
<html lang='de'>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1'>
    <title>burroQueue – Web Drop</title>
    <style>
        body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; background: #121212; color: #fff; text-align: center; padding: 24px 16px; margin: 0; }
        .card { max-width: 480px; margin: 0 auto; background: #1e1e1e; padding: 24px; border-radius: 12px; box-shadow: 0 4px 16px rgba(0,0,0,0.5); border: 1px solid #333; }
        h1 { margin: 0 0 8px 0; font-size: 24px; color: #4fc3f7; }
        .group-name { font-size: 13px; opacity: 0.7; margin-bottom: 20px; }
        .now-playing { background: #252525; padding: 12px 16px; border-radius: 8px; margin-bottom: 20px; text-align: left; border-left: 4px solid #4fc3f7; }
        .now-playing .label { font-size: 11px; text-transform: uppercase; letter-spacing: 1px; color: #4fc3f7; font-weight: bold; }
        .now-playing .title { font-size: 15px; font-weight: bold; margin-top: 4px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
        .queue-container { text-align: left; background: #252525; padding: 12px 16px; border-radius: 8px; margin-bottom: 20px; max-height: 180px; overflow-y: auto; }
        .queue-container .label { font-size: 11px; text-transform: uppercase; letter-spacing: 1px; color: #aaa; font-weight: bold; margin-bottom: 8px; }
        .queue-list { padding-left: 20px; margin: 0; font-size: 13px; color: #ddd; }
        .queue-list li { margin-bottom: 6px; }
        .empty-queue { font-size: 13px; color: #888; font-style: italic; }
        input[type=text] { width: 100%; box-sizing: border-box; padding: 12px; margin: 12px 0 16px 0; border-radius: 8px; border: 1px solid #444; background: #2a2a2a; color: #fff; font-size: 15px; }
        button { background: #4fc3f7; color: #000; border: none; padding: 12px 24px; border-radius: 8px; font-weight: bold; font-size: 15px; cursor: pointer; width: 100%; }
        footer { margin-top: 24px; font-size: 12px; opacity: 0.6; }
        footer a { color: #4fc3f7; text-decoration: none; }
    </style>
</head>
<body>
    <div class='card'>
        <h1>🫏 burroQueue</h1>");

        string groupName = _group?.Name ?? "Office Jukebox";
        sb.Append(CultureInfo.InvariantCulture, $"<div class='group-name'>Büro-Gruppe: {WebUtility.HtmlEncode(groupName)}</div>");

        sb.Append("<div class='now-playing'><div class='label'>Aktuell läuft:</div>");
        if (_group?.Playlist.CurrentTrack is not null)
        {
            sb.Append(CultureInfo.InvariantCulture, $"<div class='title'>{WebUtility.HtmlEncode(_group.Playlist.CurrentTrack.Title)}</div>");
        }
        else
        {
            sb.Append("<div class='title' style='color:#888;'>Kein Song aktiv</div>");
        }
        sb.Append("</div>");

        sb.Append("<div class='queue-container'><div class='label'>Warteschlange:</div>");
        List<Domain.Model.Track>? queued = _group?.Playlist.QueuedTracks.ToList();
        if (queued is not null && queued.Count > 0)
        {
            sb.Append("<ol class='queue-list'>");
            foreach (Domain.Model.Track t in queued)
            {
                sb.Append(CultureInfo.InvariantCulture, $"<li>{WebUtility.HtmlEncode(t.Title)}</li>");
            }
            sb.Append("</ol>");
        }
        else
        {
            sb.Append("<div class='empty-queue'>Warteschlange ist leer</div>");
        }
        sb.Append("</div>");

        sb.Append(@"<p style='font-size:14px; margin-bottom:4px;'>Wirf einen YouTube-Link ein:</p>
        <form method='POST' action='/drop'>
            <input type='text' name='url' placeholder='https://www.youtube.com/watch?v=...' required autofocus />
            <button type='submit'>In Queue werfen</button>
        </form>
        <footer>
            Lizenziert unter GNU AGPL-3.0 &bull; <a href='https://github.com/T-Boyke/burroQueue' target='_blank'>Source Code</a>
        </footer>
    </div>
</body>
</html>");

        return sb.ToString();
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
