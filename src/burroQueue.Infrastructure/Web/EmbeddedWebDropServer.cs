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
<html lang='de'>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1'>
    <title>burroQueue &ndash; Web Drop</title>
    <style>
        :root {
            --bg-page: #141416;
            --bg-card: #202023;
            --bg-subtle: #27272b;
            --border: rgba(255, 255, 255, 0.08);
            --border-focus: #60cdff;
            --text-main: #f5f5f7;
            --text-secondary: #9a9aa0;
            --accent: #60cdff;
            --accent-hover: #78d5ff;
            --accent-active: #4abce8;
            --success-bg: rgba(56, 178, 107, 0.15);
            --success-border: rgba(56, 178, 107, 0.35);
            --success-text: #5fe09a;
        }
        * { box-sizing: border-box; }
        body {
            margin: 0;
            padding: 24px 16px;
            background-color: var(--bg-page);
            color: var(--text-main);
            font-family: 'Segoe UI Variable Text', 'Segoe UI', system-ui, -apple-system, sans-serif;
            -webkit-font-smoothing: antialiased;
            display: flex;
            justify-content: center;
            align-items: flex-start;
            min-height: 100vh;
        }
        .container {
            width: 100%;
            max-width: 460px;
            background: var(--bg-card);
            border: 1px solid var(--border);
            border-radius: 12px;
            padding: 28px 24px;
            box-shadow: 0 16px 36px rgba(0, 0, 0, 0.4);
        }
        .brand-header {
            display: flex;
            align-items: center;
            justify-content: space-between;
            margin-bottom: 20px;
        }
        .brand-title {
            font-size: 20px;
            font-weight: 700;
            letter-spacing: -0.02em;
            color: var(--text-main);
            display: flex;
            align-items: center;
            gap: 8px;
        }
        .brand-accent {
            color: var(--accent);
        }
        .group-badge {
            font-size: 11px;
            font-weight: 600;
            color: var(--text-secondary);
            background: var(--bg-subtle);
            border: 1px solid var(--border);
            padding: 4px 8px;
            border-radius: 6px;
        }
        .toast {
            background: var(--success-bg);
            border: 1px solid var(--success-border);
            color: var(--success-text);
            font-size: 13px;
            font-weight: 500;
            padding: 10px 14px;
            border-radius: 8px;
            margin-bottom: 18px;
            text-align: center;
        }
        .section-card {
            background: var(--bg-subtle);
            border: 1px solid var(--border);
            border-radius: 8px;
            padding: 14px 16px;
            margin-bottom: 16px;
            text-align: left;
        }
        .section-label {
            font-size: 11px;
            font-weight: 700;
            text-transform: uppercase;
            letter-spacing: 0.08em;
            color: var(--accent);
            margin-bottom: 4px;
        }
        .now-title {
            font-size: 15px;
            font-weight: 600;
            color: var(--text-main);
            overflow: hidden;
            text-overflow: ellipsis;
            white-space: nowrap;
        }
        .now-submitter {
            font-size: 12px;
            color: var(--text-secondary);
            margin-top: 3px;
        }
        .skip-form {
            margin-top: 10px;
        }
        .btn-skip {
            background: transparent;
            color: var(--text-secondary);
            border: 1px solid var(--border);
            border-radius: 6px;
            padding: 6px 12px;
            font-size: 12px;
            font-weight: 500;
            cursor: pointer;
            transition: all 0.15s ease;
        }
        .btn-skip:hover {
            color: var(--text-main);
            border-color: rgba(255, 255, 255, 0.2);
            background: rgba(255, 255, 255, 0.04);
        }
        .btn-skip:active {
            transform: scale(0.98);
        }
        .queue-container {
            max-height: 140px;
            overflow-y: auto;
            margin-top: 8px;
        }
        .queue-list {
            margin: 0;
            padding-left: 20px;
            font-size: 13px;
            color: var(--text-main);
        }
        .queue-list li {
            margin-bottom: 4px;
        }
        .queue-empty {
            font-size: 13px;
            color: var(--text-secondary);
            font-style: italic;
        }
        .form-group {
            margin-bottom: 12px;
            text-align: left;
        }
        .form-label {
            display: block;
            font-size: 12px;
            font-weight: 600;
            color: var(--text-secondary);
            margin-bottom: 6px;
        }
        .input-text {
            width: 100%;
            background: #19191b;
            border: 1px solid var(--border);
            border-radius: 6px;
            padding: 10px 12px;
            color: var(--text-main);
            font-size: 14px;
            font-family: inherit;
            outline: none;
            transition: border-color 0.15s ease;
        }
        .input-text:focus {
            border-color: var(--border-focus);
        }
        .input-text::placeholder {
            color: rgba(255, 255, 255, 0.3);
        }
        .btn-primary {
            width: 100%;
            background: var(--accent);
            color: #0b0f14;
            border: none;
            border-radius: 6px;
            padding: 12px;
            font-size: 14px;
            font-weight: 600;
            cursor: pointer;
            font-family: inherit;
            transition: background 0.15s ease, transform 0.05s ease;
            margin-top: 6px;
        }
        .btn-primary:hover {
            background: var(--accent-hover);
        }
        .btn-primary:active {
            background: var(--accent-active);
            transform: scale(0.99);
        }
        .footer-note {
            margin-top: 24px;
            font-size: 11px;
            color: var(--text-secondary);
            text-align: center;
        }
        .footer-note a {
            color: var(--text-secondary);
            text-decoration: underline;
        }
    </style>
</head>
<body>
    <div class='container'>
        <div class='brand-header'>
            <div class='brand-title'>
                <span class='brand-accent'>burro</span>Queue
            </div>");

        sb.Append(CultureInfo.InvariantCulture, $"<div class='group-badge'>{WebUtility.HtmlEncode(groupName)}</div></div>");

        if (isDropped)
        {
            sb.Append("<div class='toast'>Titel erfolgreich zur Warteschlange hinzugefügt.</div>");
        }
        else if (isVoted)
        {
            sb.Append("<div class='toast'>Deine Stimme für Skip wurde gezählt.</div>");
        }

        sb.Append("<div class='section-card'>");
        sb.Append("<div class='section-label'>Aktuell l&auml;uft</div>");

        Domain.Model.Track? currentTrack = _group?.Playlist.CurrentTrack;
        if (currentTrack is not null)
        {
            sb.Append(CultureInfo.InvariantCulture, $"<div class='now-title' id='now-playing-title'>{WebUtility.HtmlEncode(currentTrack.Title)}</div>");
            sb.Append(CultureInfo.InvariantCulture, $"<div class='now-submitter' id='now-playing-submitter'>Eingeworfen von: {WebUtility.HtmlEncode(currentTrack.SubmittedBy)}</div>");
        }
        else
        {
            sb.Append("<div class='now-title' id='now-playing-title' style='color:var(--text-secondary);'>Kein Song aktiv</div>");
            sb.Append("<div class='now-submitter' id='now-playing-submitter'></div>");
        }

        string voteLabel = !string.IsNullOrWhiteSpace(skipVoteStatus) ? $" ({WebUtility.HtmlEncode(skipVoteStatus)})" : string.Empty;
        string skipBtnDisplay = currentTrack is not null ? "block" : "none";
        sb.Append(CultureInfo.InvariantCulture, $@"<form method='POST' action='/vote-skip' class='skip-form' id='skip-form' style='display:{skipBtnDisplay};'>
            <button type='submit' class='btn-skip' id='skip-btn'>F&uuml;r Skip stimmen{voteLabel}</button>
        </form>
        </div>");

        sb.Append(@"<div class='section-card'>
            <div class='section-label'>Warteschlange</div>
            <div class='queue-container'>");

        List<Domain.Model.Track>? queued = _group?.Playlist.QueuedTracks.ToList();
        bool hasQueued = queued is not null && queued.Count > 0;
        string queueListDisplay = hasQueued ? "block" : "none";
        string queueEmptyDisplay = hasQueued ? "none" : "block";

        sb.Append(CultureInfo.InvariantCulture, $"<ol class='queue-list' id='queue-list' style='display:{queueListDisplay};'>");
        if (queued is not null)
        {
            foreach (Domain.Model.Track t in queued)
            {
                sb.Append(CultureInfo.InvariantCulture, $"<li>{WebUtility.HtmlEncode(t.Title)} ({WebUtility.HtmlEncode(t.SubmittedBy)})</li>");
            }
        }
        sb.Append("</ol>");
        sb.Append(CultureInfo.InvariantCulture, $"<div class='queue-empty' id='queue-empty' style='display:{queueEmptyDisplay};'>Warteschlange ist leer</div>");
        sb.Append("</div></div>");

        sb.Append(@"<form method='POST' action='/drop'>
            <div class='form-group'>
                <label for='submitter' class='form-label'>Dein Name (optional)</label>
                <input type='text' id='submitter' name='submitter' placeholder='z. B. Alex' class='input-text' maxlength='40' />
            </div>
            <div class='form-group'>
                <label for='url' class='form-label'>YouTube-Link</label>
                <input type='url' id='url' name='url' placeholder='https://www.youtube.com/watch?v=...' required autofocus class='input-text' />
            </div>
            <button type='submit' class='btn-primary'>In Warteschlange werfen</button>
        </form>

        <div class='footer-note'>
            burroQueue &bull; Lizenziert unter GNU AGPL-3.0 &bull; <a href='https://github.com/T-Boyke/burroQueue' target='_blank' rel='noopener'>GitHub</a>
        </div>
    </div>

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
                            titleEl.style.color = 'var(--text-main)';
                        }
                        if (subEl) subEl.textContent = 'Eingeworfen von: ' + (data.currentTrack.submittedBy || 'Anonym');
                        if (skipForm) skipForm.style.display = 'block';
                        if (skipBtn) skipBtn.textContent = 'F\u00FCr Skip stimmen' + (data.skipVotes ? ' (' + data.skipVotes + ')' : '');
                    } else {
                        if (titleEl) {
                            titleEl.textContent = 'Kein Song aktiv';
                            titleEl.style.color = 'var(--text-secondary)';
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

