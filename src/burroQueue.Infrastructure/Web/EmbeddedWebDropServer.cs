using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

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

    public Func<string>? SkipVoteStatusProvider { get; set; }

    public event EventHandler<LinkDropRequest>? LinkDropped;

    public event EventHandler? SkipVoteRequested;

    private readonly IQrCodeSvgGenerator _qrCodeGenerator;

    public EmbeddedWebDropServer(
        int port = 8080,
        Domain.Model.BurroGroup? group = null,
        IQrCodeSvgGenerator? qrCodeGenerator = null)
    {
        _configuredPort = port;
        _actualPort = port;
        _group = group;
        _qrCodeGenerator = qrCodeGenerator ?? new QrCodeSvgGenerator();
    }


    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (IsRunning)
        {
            return Task.CompletedTask;
        }

        int[] candidatePorts = _configuredPort == 8080
            ? [8080, 8081, 8082, 8088, 18080, 0]
            : [_configuredPort];

        bool started = false;
        foreach (int candidatePort in candidatePorts)
        {
            if (TryStartListener(IPAddress.Any, candidatePort))
            {
                started = true;
                break;
            }
        }

        if (!started)
        {
            foreach (int candidatePort in candidatePorts)
            {
                if (TryStartListener(IPAddress.Loopback, candidatePort))
                {
                    started = true;
                    break;
                }
            }
        }

        if (!started)
        {
            return Task.CompletedTask;
        }

        _isRunning = true;
        _cts = new CancellationTokenSource();
        _listenTask = Task.Run(() => AcceptLoopAsync(_cts.Token), CancellationToken.None);
        return Task.CompletedTask;
    }

    private bool TryStartListener(IPAddress bindAddress, int port)
    {
        try
        {
            TcpListener listener = new(bindAddress, port);
            listener.Start();
            _tcpListener = listener;
            if (listener.LocalEndpoint is IPEndPoint ep)
            {
                _actualPort = ep.Port;
            }

            return true;
        }
        catch
        {
            return false;
        }
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
                string? hostHeader = null;

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
                        else if (headerName.Equals("Host", StringComparison.OrdinalIgnoreCase))
                        {
                            hostHeader = headerValue;
                        }
                    }
                }

                if (method == "POST" && (path == "/drop" || path.StartsWith("/drop?", StringComparison.Ordinal)))
                {
                    string body = await ReadBodyAsync(reader, contentLength).ConfigureAwait(false);
                    string submittedUrl = body;
                    string? submittedBy = null;

                    if (contentType?.Contains("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        string[] pairs = body.Split('&');
                        foreach (string pair in pairs)
                        {
                            string[] kvp = pair.Split('=', 2);
                            if (kvp.Length == 2)
                            {
                                string key = WebUtility.UrlDecode(kvp[0]).Trim();
                                string val = WebUtility.UrlDecode(kvp[1]).Trim();
                                if (key.Equals("url", StringComparison.OrdinalIgnoreCase))
                                {
                                    submittedUrl = val;
                                }
                                else if (key.Equals("submitter", StringComparison.OrdinalIgnoreCase))
                                {
                                    submittedBy = string.IsNullOrWhiteSpace(val) ? null : val;
                                }
                            }
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(submittedUrl))
                    {
                        LinkDropped?.Invoke(this, new LinkDropRequest(submittedUrl.Trim(), submittedBy));
                    }

                    byte[] headerBytes = Encoding.ASCII.GetBytes("HTTP/1.1 303 See Other\r\nLocation: /?status=dropped\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
                    await stream.WriteAsync(headerBytes, token).ConfigureAwait(false);
                    await stream.FlushAsync(token).ConfigureAwait(false);
                }
                else if (method == "POST" && (path == "/vote-skip" || path.StartsWith("/vote-skip?", StringComparison.Ordinal)))
                {
                    SkipVoteRequested?.Invoke(this, EventArgs.Empty);

                    byte[] headerBytes = Encoding.ASCII.GetBytes("HTTP/1.1 303 See Other\r\nLocation: /?status=voted\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
                    await stream.WriteAsync(headerBytes, token).ConfigureAwait(false);
                    await stream.FlushAsync(token).ConfigureAwait(false);
                }
                else if (method == "GET" && (path == "/qr.svg" || path.StartsWith("/qr.svg?", StringComparison.Ordinal)))
                {
                    string host = !string.IsNullOrWhiteSpace(hostHeader) ? hostHeader : $"localhost:{_actualPort}";
                    string targetUrl = host.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || host.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                        ? host
                        : $"http://{host}/";
                    string svg = _qrCodeGenerator.GenerateSvg(targetUrl, moduleSize: 4);
                    byte[] svgBytes = Encoding.UTF8.GetBytes(svg);
                    byte[] headerBytes = Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: image/svg+xml; charset=utf-8\r\nContent-Length: {svgBytes.Length}\r\nConnection: close\r\n\r\n");

                    await stream.WriteAsync(headerBytes, token).ConfigureAwait(false);
                    await stream.WriteAsync(svgBytes, token).ConfigureAwait(false);
                    await stream.FlushAsync(token).ConfigureAwait(false);
                }
                else if (method == "GET" && (path == "/assets/webdrop.css" || path.StartsWith("/assets/webdrop.css?", StringComparison.Ordinal)))
                {
                    ReadOnlyMemory<byte> cssBytes = WebDropStylesheetProvider.GetCssUtf8Bytes();
                    byte[] headerBytes = Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: text/css; charset=utf-8\r\nCache-Control: public, max-age=3600\r\nContent-Length: {cssBytes.Length}\r\nConnection: close\r\n\r\n");

                    await stream.WriteAsync(headerBytes, token).ConfigureAwait(false);
                    await stream.WriteAsync(cssBytes, token).ConfigureAwait(false);
                    await stream.FlushAsync(token).ConfigureAwait(false);
                }
                else if (method == "GET" && (path == "/api/status" || path.StartsWith("/api/status?", StringComparison.Ordinal)))
                {
                    string statusJson = GenerateStatusJson();
                    byte[] jsonBytes = Encoding.UTF8.GetBytes(statusJson);
                    byte[] headerBytes = Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: application/json; charset=utf-8\r\nContent-Length: {jsonBytes.Length}\r\nConnection: close\r\n\r\n");

                    await stream.WriteAsync(headerBytes, token).ConfigureAwait(false);
                    await stream.WriteAsync(jsonBytes, token).ConfigureAwait(false);
                    await stream.FlushAsync(token).ConfigureAwait(false);
                }
                else
                {
                    string pageHtml = GeneratePageHtml(path);
                    byte[] pageBytes = Encoding.UTF8.GetBytes(pageHtml);
                    byte[] headerBytes = Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: {pageBytes.Length}\r\nConnection: close\r\n\r\n");

                    await stream.WriteAsync(headerBytes, token).ConfigureAwait(false);
                    await stream.WriteAsync(pageBytes, token).ConfigureAwait(false);
                    await stream.FlushAsync(token).ConfigureAwait(false);
                }

            }
            catch (Exception ex) when (ex is IOException or SocketException or OperationCanceledException)
            {
                // Client-Disconnect ignorieren
            }
        }
    }

    private static async Task<string> ReadBodyAsync(StreamReader reader, int contentLength)
    {
        if (contentLength <= 0)
        {
            return string.Empty;
        }

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
        return new string(buffer, 0, readTotal);
    }

    public string GenerateStatusJson()
    {
        string groupName = _group?.Name ?? "Office Jukebox";
        string? skipVotes = SkipVoteStatusProvider?.Invoke();

        using MemoryStream ms = new();
        using (Utf8JsonWriter writer = new(ms))
        {
            writer.WriteStartObject();
            writer.WriteString("groupName", groupName);
            writer.WriteString("skipVotes", skipVotes ?? string.Empty);

            Domain.Model.Track? current = _group?.Playlist.CurrentTrack;
            if (current is not null)
            {
                writer.WriteStartObject("currentTrack");
                writer.WriteString("title", current.Title);
                writer.WriteString("submittedBy", current.SubmittedBy);
                writer.WriteEndObject();
            }
            else
            {
                writer.WriteNull("currentTrack");
            }

            writer.WriteStartArray("queuedTracks");
            if (_group?.Playlist.QueuedTracks is not null)
            {
                foreach (Domain.Model.Track track in _group.Playlist.QueuedTracks)
                {
                    writer.WriteStartObject();
                    writer.WriteString("title", track.Title);
                    writer.WriteString("submittedBy", track.SubmittedBy);
                    writer.WriteEndObject();
                }
            }
            writer.WriteEndArray();

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(ms.ToArray());
    }

    public string GeneratePageHtml(string? path = null)
    {
        StringBuilder sb = new();
        string groupName = _group?.Name ?? "Office Jukebox";
        string? skipVoteStatus = SkipVoteStatusProvider?.Invoke();

        bool isDropped = path?.Contains("status=dropped", StringComparison.OrdinalIgnoreCase) == true;
        bool isVoted = path?.Contains("status=voted", StringComparison.OrdinalIgnoreCase) == true;

        sb.Append(@"<!DOCTYPE html>
<html lang='de' data-style-engine='unocss-66.12.0'>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1'>
    <title>burroQueue &ndash; Web Drop</title>
    <link rel='stylesheet' href='/assets/webdrop.css'>
</head>
<body class='bq-page'>
    <main class='bq-shell'>
        <header class='flex items-center justify-between mb-5'>
            <div class='text-xl font-bold tracking-tight text-fluent-text flex items-center gap-1'>
                <span><span class='text-fluent-accent'>burro</span>Queue</span>
            </div>");

        sb.Append(CultureInfo.InvariantCulture, $"<div class='bq-badge'>{WebUtility.HtmlEncode(groupName)}</div></header>");

        if (isDropped)
        {
            sb.Append("<div class='bq-toast'>Titel erfolgreich zur Warteschlange hinzugefügt.</div>");
        }
        else if (isVoted)
        {
            sb.Append("<div class='bq-toast'>Deine Stimme für Skip wurde gezählt.</div>");
        }

        sb.Append("<section class='bq-section'>");
        sb.Append("<div class='bq-section-label'>Aktuell l&auml;uft</div>");

        Domain.Model.Track? currentTrack = _group?.Playlist.CurrentTrack;
        if (currentTrack is not null)
        {
            sb.Append(CultureInfo.InvariantCulture, $"<div class='text-[15px] font-semibold text-fluent-text truncate' id='now-playing-title'>{WebUtility.HtmlEncode(currentTrack.Title)}</div>");
            sb.Append(CultureInfo.InvariantCulture, $"<div class='text-xs text-fluent-muted mt-1' id='now-playing-submitter'>Eingeworfen von: {WebUtility.HtmlEncode(currentTrack.SubmittedBy)}</div>");
        }
        else
        {
            sb.Append("<div class='text-[15px] font-semibold text-fluent-muted truncate' id='now-playing-title'>Kein Song aktiv</div>");
            sb.Append("<div class='text-xs text-fluent-muted mt-1' id='now-playing-submitter'></div>");
        }

        string voteLabel = !string.IsNullOrWhiteSpace(skipVoteStatus) ? $" ({WebUtility.HtmlEncode(skipVoteStatus)})" : string.Empty;
        string skipBtnDisplay = currentTrack is not null ? "block" : "none";
        sb.Append(CultureInfo.InvariantCulture, $@"<form method='POST' action='/vote-skip' class='mt-2.5' id='skip-form' style='display:{skipBtnDisplay};'>
            <button type='submit' class='bq-btn-skip' id='skip-btn'>F&uuml;r Skip stimmen{voteLabel}</button>
        </form>
        </section>");

        sb.Append(@"<section class='bq-section'>
            <div class='bq-section-label'>Warteschlange</div>
            <div class='max-h-[140px] overflow-y-auto mt-2'>");

        List<Domain.Model.Track>? queued = _group?.Playlist.QueuedTracks.ToList();
        bool hasQueued = queued is not null && queued.Count > 0;
        string queueListDisplay = hasQueued ? "block" : "none";
        string queueEmptyDisplay = hasQueued ? "none" : "block";

        sb.Append(CultureInfo.InvariantCulture, $"<ol class='m-0 pl-5 text-[13px] text-fluent-text space-y-1' id='queue-list' style='display:{queueListDisplay};'>");
        if (queued is not null)
        {
            foreach (Domain.Model.Track t in queued)
            {
                sb.Append(CultureInfo.InvariantCulture, $"<li class='mb-1'>{WebUtility.HtmlEncode(t.Title)} ({WebUtility.HtmlEncode(t.SubmittedBy)})</li>");
            }
        }
        sb.Append("</ol>");
        sb.Append(CultureInfo.InvariantCulture, $"<div class='text-[13px] text-fluent-muted italic' id='queue-empty' style='display:{queueEmptyDisplay};'>Warteschlange ist leer</div>");
        sb.Append("</div></section>");

        sb.Append(@"<div class='flex items-center gap-3 bg-fluent-subtle border border-solid border-fluent-border rounded-lg px-3 py-2 mb-3.5'>
            <img src='/qr.svg' width='48' height='48' class='rounded bg-white p-0.5 shrink-0' alt='QR-Code' />
            <div>
                <div class='text-xs font-semibold text-fluent-text'>Smartphone Direkt-Drop</div>
                <div class='text-[11px] text-fluent-muted'>QR-Code mit dem Handy scannen, um Songs mobil einzuwerfen.</div>
            </div>
        </div>");

        sb.Append(@"<form method='POST' action='/drop'>
            <div class='mb-3 text-left'>
                <label for='submitter' class='block text-xs font-semibold text-fluent-muted mb-1.5'>Dein Name (optional)</label>
                <input type='text' id='submitter' name='submitter' placeholder='z. B. Alex' class='bq-input' maxlength='40' />
            </div>
            <div class='mb-3 text-left'>
                <label for='url' class='block text-xs font-semibold text-fluent-muted mb-1.5'>YouTube-Link</label>
                <input type='url' id='url' name='url' placeholder='https://www.youtube.com/watch?v=...' required autofocus class='bq-input' />
            </div>
            <button type='submit' class='bq-btn-primary'>In Warteschlange werfen</button>
        </form>

        <footer class='mt-6 text-[11px] text-fluent-muted text-center'>
            burroQueue &bull; Lizenziert unter GNU AGPL-3.0 &bull; <a href='https://github.com/T-Boyke/burroQueue' target='_blank' rel='noopener' class='text-fluent-muted underline hover:text-fluent-text'>GitHub</a>
        </footer>
    </main>

    <script>
        const nameField = document.getElementById('submitter');
        if (nameField) {
            const savedName = localStorage.getItem('burro_submitter_name');
            if (savedName) nameField.value = savedName;
            nameField.addEventListener('input', () => {
                localStorage.setItem('burro_submitter_name', nameField.value.trim());
            });
        }

        function pollStatus() {
            fetch('/api/status')
                .then(r => r.json())
                .then(data => {
                    const titleEl = document.getElementById('now-playing-title');
                    const subEl = document.getElementById('now-playing-submitter');
                    const skipForm = document.getElementById('skip-form');
                    const skipBtn = document.getElementById('skip-btn');
                    const listEl = document.getElementById('queue-list');
                    const emptyEl = document.getElementById('queue-empty');

                    if (data.currentTrack) {
                        if (titleEl) {
                            titleEl.textContent = data.currentTrack.title;
                            titleEl.classList.remove('text-fluent-muted');
                            titleEl.classList.add('text-fluent-text');
                        }
                        if (subEl) subEl.textContent = 'Eingeworfen von: ' + (data.currentTrack.submittedBy || 'Anonym');
                        if (skipForm) skipForm.style.display = 'block';
                        if (skipBtn) skipBtn.textContent = 'F\u00FCr Skip stimmen' + (data.skipVotes ? ' (' + data.skipVotes + ')' : '');
                    } else {
                        if (titleEl) {
                            titleEl.textContent = 'Kein Song aktiv';
                            titleEl.classList.remove('text-fluent-text');
                            titleEl.classList.add('text-fluent-muted');
                        }
                        if (subEl) subEl.textContent = '';
                        if (skipForm) skipForm.style.display = 'none';
                    }

                    if (listEl && emptyEl) {
                        if (data.queuedTracks && data.queuedTracks.length > 0) {
                            emptyEl.style.display = 'none';
                            listEl.style.display = 'block';
                            listEl.innerHTML = '';
                            data.queuedTracks.forEach(t => {
                                const li = document.createElement('li');
                                li.className = 'mb-1';
                                li.textContent = t.title + ' (' + (t.submittedBy || 'Anonym') + ')';
                                listEl.appendChild(li);
                            });
                        } else {
                            listEl.style.display = 'none';
                            emptyEl.style.display = 'block';
                        }
                    }
                })
                .catch(() => {});
        }
        setInterval(pollStatus, 5000);
    </script>
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

