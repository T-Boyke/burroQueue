using System.Net;
using System.Text;
using burroQueue.Domain.Model;

namespace burroQueue.Infrastructure.Telephony;

public sealed class StarfaceTelephonyProvider : ITelephonyProvider
{
    private readonly int _port;
    private HttpListener? _httpListener;
    private CancellationTokenSource? _cts;
    private Task? _listenTask;
    private TelephonyState _currentState = TelephonyState.Idle;
    private bool _isDisposed;

    public string ProviderId => "Starface";

    public string DisplayName => "STARFACE VoIP (Call Actions / Webhook)";

    public bool IsAvailable => OperatingSystem.IsWindows();

    public event EventHandler<TelephonyCallEventArgs>? CallStateChanged;

    public StarfaceTelephonyProvider(int port = 41235)
    {
        _port = port;
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_httpListener is { IsListening: true })
        {
            return Task.CompletedTask;
        }

        try
        {
            _cts = new CancellationTokenSource();
            _httpListener = new HttpListener();
            _httpListener.Prefixes.Add($"http://127.0.0.1:{_port}/starface/");
            _httpListener.Start();

            _listenTask = Task.Run(() => ListenLoopAsync(_cts.Token), CancellationToken.None);
        }
        catch (HttpListenerException)
        {
            // Port might be in use or access restricted
        }

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_cts is not null)
        {
            await _cts.CancelAsync().ConfigureAwait(false);
        }

        if (_httpListener is not null)
        {
            try
            {
                _httpListener.Stop();
                _httpListener.Close();
            }
            catch (ObjectDisposedException)
            {
            }

            _httpListener = null;
        }

        if (_listenTask is not null)
        {
            try
            {
                await _listenTask.ConfigureAwait(false);
            }
            catch (Exception)
            {
            }

            _listenTask = null;
        }

        _currentState = TelephonyState.Idle;
    }

    public void TriggerStateManually(TelephonyState state)
    {
        _currentState = state;
        CallStateChanged?.Invoke(this, new TelephonyCallEventArgs(ProviderId, state));
    }

    public static TelephonyState MapQueryToTelephonyState(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return TelephonyState.Idle;
        }

        string normalized = query.TrimStart('?').ToLowerInvariant();

        if (normalized.Contains("ringing") ||
            normalized.Contains("klingeln") ||
            normalized.Contains("incoming") ||
            normalized.Contains("alerting"))
        {
            return TelephonyState.Ringing;
        }

        if (normalized.Contains("connected") ||
            normalized.Contains("active") ||
            normalized.Contains("verbunden") ||
            normalized.Contains("gespraech"))
        {
            return TelephonyState.ActiveCall;
        }

        if (normalized.Contains("connecting") ||
            normalized.Contains("waehlt") ||
            normalized.Contains("dialing"))
        {
            return TelephonyState.Connecting;
        }

        return TelephonyState.Idle;
    }

    private async Task ListenLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested && _httpListener is { IsListening: true })
        {
            try
            {
                HttpListenerContext context = await _httpListener.GetContextAsync().WaitAsync(token).ConfigureAwait(false);
                ProcessIncomingRequest(context);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (HttpListenerException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception)
            {
                // Unhandled server errors ignored to keep listener alive
            }
        }
    }

    private void ProcessIncomingRequest(HttpListenerContext context)
    {
        try
        {
            string? query = context.Request.Url?.Query;
            TelephonyState newState = MapQueryToTelephonyState(query);

            _currentState = newState;
            CallStateChanged?.Invoke(this, new TelephonyCallEventArgs(ProviderId, newState));

            byte[] responseBytes = Encoding.UTF8.GetBytes(
                $"{{\"status\":\"ok\",\"provider\":\"Starface\",\"state\":\"{newState}\"}}");

            context.Response.ContentType = "application/json; charset=utf-8";
            context.Response.ContentLength64 = responseBytes.Length;
            context.Response.StatusCode = (int)HttpStatusCode.OK;
            context.Response.OutputStream.Write(responseBytes, 0, responseBytes.Length);
            context.Response.OutputStream.Close();
        }
        catch (Exception)
        {
            try
            {
                context.Response.Abort();
            }
            catch
            {
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

        if (_cts is not null)
        {
            _cts.Dispose();
            _cts = null;
        }
    }
}

