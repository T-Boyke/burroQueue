using System.Globalization;
using System.Net;
using System.Text;

namespace burroQueue.Infrastructure.Web;

public sealed class EmbeddedWebDropServer : IEmbeddedWebDropServer
{
    private HttpListener? _listener;
    private readonly Domain.Model.BurroGroup? _group;
    private CancellationTokenSource? _cts;
    private Task? _listenTask;
    private bool _isDisposed;

    public int Port { get; }

    public bool IsRunning => _listener is { IsListening: true };

    public event EventHandler<string>? LinkDropped;

    public EmbeddedWebDropServer(int port = 8080, Domain.Model.BurroGroup? group = null)
    {
        Port = port;
        _group = group;
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_listener is { IsListening: true })
        {
            return Task.CompletedTask;
        }

        HttpListener listener = new();
        try
        {
            listener.Prefixes.Add($"http://*:{Port}/");
            listener.Start();
        }
        catch
        {
            try
            {
                listener.Close();
            }
            catch
            {
            }

            listener = new HttpListener();
            listener.Prefixes.Add($"http://localhost:{Port}/");
            try
            {
                listener.Start();
            }
            catch
            {
                try
                {
                    listener.Close();
                }
                catch
                {
                }

                return Task.CompletedTask;
            }
        }

        _listener = listener;
        _cts = new CancellationTokenSource();
        _listenTask = Task.Run(() => ListenLoopAsync(_cts.Token), CancellationToken.None);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_cts is not null)
        {
            await _cts.CancelAsync().ConfigureAwait(false);
        }

        if (_listener is { IsListening: true })
        {
            try
            {
                _listener.Stop();
            }
            catch
            {
                // Ignoriere Fehler beim Stoppen
            }
        }

        if (_listenTask is not null)
        {
            try
            {
                await _listenTask.ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is OperationCanceledException or HttpListenerException or InvalidOperationException or ObjectDisposedException)
            {
                // Normal bei Shutdown/Cancellation
            }
        }
    }

    private async Task ListenLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested && _listener is { IsListening: true })
        {
            try
            {
                HttpListenerContext context = await _listener.GetContextAsync().ConfigureAwait(false);
                _ = Task.Run(() => HandleRequestAsync(context), CancellationToken.None);
            }
            catch (Exception ex) when (ex is HttpListenerException or InvalidOperationException or ObjectDisposedException or OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext context)
    {
        HttpListenerRequest req = context.Request;
        HttpListenerResponse res = context.Response;

        try
        {
            if (req.HttpMethod == "POST" && req.Url?.AbsolutePath == "/drop")
            {
                using StreamReader reader = new(req.InputStream, req.ContentEncoding);
                string body = await reader.ReadToEndAsync().ConfigureAwait(false);

                string submittedUrl = body;
                if (req.ContentType?.Contains("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase) == true)
                {
                    string[] parts = body.Split('&');
                    foreach (string p in parts)
                    {
                        string[] kvp = p.Split('=', 2);
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

                byte[] respBytes = Encoding.UTF8.GetBytes("<html><body style='font-family:sans-serif;text-align:center;padding:40px;background:#121212;color:#fff;'><h2>Song eingeworfen!</h2><p>Dein Link läuft gleich auf der Jukebox.</p><a href='/' style='color:#4fc3f7;'>Weiteren Link einwerfen</a></body></html>");
                res.ContentType = "text/html; charset=utf-8";
                res.StatusCode = (int)HttpStatusCode.OK;
                await res.OutputStream.WriteAsync(respBytes).ConfigureAwait(false);
            }
            else
            {
                string html = GeneratePageHtml();
                byte[] bytes = Encoding.UTF8.GetBytes(html);
                res.ContentType = "text/html; charset=utf-8";
                res.StatusCode = (int)HttpStatusCode.OK;
                await res.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
            }
        }
        catch
        {
            res.StatusCode = (int)HttpStatusCode.InternalServerError;
        }
        finally
        {
            res.Close();
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
        try
        {
            _listener?.Close();
        }
        catch
        {
        }
        _cts?.Dispose();
    }
}

