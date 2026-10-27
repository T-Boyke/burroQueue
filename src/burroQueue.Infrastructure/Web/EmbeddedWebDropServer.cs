using System.Net;
using System.Text;

namespace burroQueue.Infrastructure.Web;

public sealed class EmbeddedWebDropServer : IEmbeddedWebDropServer
{
    private readonly HttpListener _listener;
    private CancellationTokenSource? _cts;
    private Task? _listenTask;
    private bool _isDisposed;

    public int Port { get; }

    public bool IsRunning => _listener.IsListening;

    public event EventHandler<string>? LinkDropped;

    public EmbeddedWebDropServer(int port = 8080)
    {
        Port = port;
        _listener = new HttpListener();
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_listener.IsListening)
        {
            return Task.CompletedTask;
        }

        try
        {
            _listener.Prefixes.Clear();
            _listener.Prefixes.Add($"http://*:{Port}/");
            _listener.Start();
        }
        catch (HttpListenerException)
        {
            // Fallback auf localhost falls keine Admin-URL-ACL existiert
            _listener.Prefixes.Clear();
            _listener.Prefixes.Add($"http://localhost:{Port}/");
            try
            {
                _listener.Start();
            }
            catch
            {
                return Task.CompletedTask;
            }
        }

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

        if (_listener.IsListening)
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
            catch (OperationCanceledException)
            {
                // Normal
            }
        }
    }

    private async Task ListenLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested && _listener.IsListening)
        {
            try
            {
                HttpListenerContext context = await _listener.GetContextAsync().ConfigureAwait(false);
                _ = Task.Run(() => HandleRequestAsync(context), CancellationToken.None);
            }
            catch (HttpListenerException)
            {
                break;
            }
            catch (OperationCanceledException)
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
                string html = @"<!DOCTYPE html>
<html>
<head>
    <meta name='viewport' content='width=device-width, initial-scale=1'>
    <title>burroQueue – Web Drop</title>
    <style>
        body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; background: #121212; color: #fff; text-align: center; padding: 24px; }
        .card { max-width: 420px; margin: 0 auto; background: #1e1e1e; padding: 24px; border-radius: 12px; box-shadow: 0 4px 12px rgba(0,0,0,0.5); }
        input[type=text] { width: 90%; padding: 12px; margin: 16px 0; border-radius: 8px; border: 1px solid #333; background: #2a2a2a; color: #fff; font-size: 16px; }
        button { background: #4fc3f7; color: #000; border: none; padding: 12px 24px; border-radius: 8px; font-weight: bold; font-size: 16px; cursor: pointer; }
        footer { margin-top: 24px; font-size: 12px; opacity: 0.6; }
        footer a { color: #aaa; }
    </style>
</head>
<body>
    <div class='card'>
        <h1>burroQueue</h1>
        <p>Wirf einen YouTube-Link in die Office-Jukebox:</p>
        <form method='POST' action='/drop'>
            <input type='text' name='url' placeholder='https://www.youtube.com/watch?v=...' required autofocus />
            <br />
            <button type='submit'>In Queue werfen</button>
        </form>
        <footer>
            Lizenziert unter GNU AGPL-3.0 &bull; <a href='https://github.com/T-Boyke/burroQueue' target='_blank'>Source Code</a>
        </footer>
    </div>
</body>
</html>";
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
