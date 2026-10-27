using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using burroQueue.Domain.Model;

namespace burroQueue.Infrastructure.Telephony;

[SupportedOSPlatform("windows")]
public sealed class SwyxItTelephonyProvider : ITelephonyProvider
{
    private const string _progId = "CLMgr.ClientLineMgr";
    private readonly Timer _pollingTimer;
    private dynamic? _clMgr;
    private TelephonyState _lastState = TelephonyState.Idle;
    private bool _isDisposed;

    public string ProviderId => "SwyxIt";

    public string DisplayName => "SwyxIt! 11 (Standard)";

    public bool IsAvailable
    {
        get
        {
            Type? type = Type.GetTypeFromProgID(_progId);
            return type is not null;
        }
    }

    public event EventHandler<TelephonyCallEventArgs>? CallStateChanged;

    public SwyxItTelephonyProvider()
    {
        _pollingTimer = new Timer(CheckLinesCallback, null, Timeout.Infinite, Timeout.Infinite);
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        EnsureConnected();
        _pollingTimer.Change(TimeSpan.Zero, TimeSpan.FromMilliseconds(500));
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        _pollingTimer.Change(Timeout.Infinite, Timeout.Infinite);
        ReleaseCom();
        return Task.CompletedTask;
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    internal static object? DefaultComActivator(string progId)
    {
        Type? type = Type.GetTypeFromProgID(progId);
        return type is not null ? Activator.CreateInstance(type) : null;
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    internal static void DefaultComReleaser(object obj)
    {
        if (Marshal.IsComObject(obj))
        {
            Marshal.ReleaseComObject(obj);
        }
    }

    internal static Func<string, object?> ComActivator { get; set; } = DefaultComActivator;
    internal static Action<object> ComReleaser { get; set; } = DefaultComReleaser;

    private void EnsureConnected()
    {
        if (_clMgr is not null)
        {
            return;
        }

        try
        {
            _clMgr = ComActivator(_progId);
        }
        catch (COMException)
        {
            _clMgr = null;
        }
    }

    internal dynamic? ClientManager
    {
        get => _clMgr;
        set => _clMgr = value;
    }

    internal void CheckLinesCallback(object? state)
    {
        if (_isDisposed)
        {
            return;
        }

        try
        {
            EnsureConnected();
            if (_clMgr is null)
            {
                UpdateState(TelephonyState.Idle);
                return;
            }

            int numberOfLines = (int)_clMgr.DispNumberOfLines;
            TelephonyState highestState = EvaluateLines(numberOfLines, i =>
            {
                dynamic? line = _clMgr.DispGetLine(i);
                return line is not null ? (int)line.DispState : null;
            });

            UpdateState(highestState);
        }
        catch (COMException)
        {
            ReleaseCom();
            UpdateState(TelephonyState.Idle);
        }
        catch (Exception)
        {
            UpdateState(TelephonyState.Idle);
        }
    }

    internal static TelephonyState EvaluateLines(int numberOfLines, Func<int, int?> getLineState)
    {
        TelephonyState highestState = TelephonyState.Idle;

        for (int i = 0; i < numberOfLines; i++)
        {
            int? lineState = getLineState(i);
            if (lineState is null)
            {
                continue;
            }

            TelephonyState mapped = MapLineStateToTelephonyState(lineState.Value);
            if (mapped > highestState)
            {
                highestState = mapped;
            }
        }

        return highestState;
    }

    public static TelephonyState MapLineStateToTelephonyState(int clMgrLineState)
    {
        // Aus Interop.CLMgr.dll:
        // LSRinging = 3, LSHookOffInternal = 1, LSHookOffExternal = 2, LSDialing = 4, LSAlerting = 5,
        // LSKnocking = 6, LSActive = 8, LSConferenceActive = 10
        return clMgrLineState switch
        {
            3 => TelephonyState.Ringing,
            1 or 2 or 4 or 5 or 6 or 24 or 26 => TelephonyState.Connecting,
            8 or 10 or 15 => TelephonyState.ActiveCall,
            _ => TelephonyState.Idle
        };
    }

    private void UpdateState(TelephonyState newState)
    {
        if (_lastState == newState)
        {
            return;
        }

        _lastState = newState;
        CallStateChanged?.Invoke(this, new TelephonyCallEventArgs(ProviderId, newState));
    }

    private void ReleaseCom()
    {
        if (_clMgr is not null)
        {
            try
            {
                ComReleaser(_clMgr);
            }
            catch
            {
                // Unhandled release errors ignored
            }
            finally
            {
                _clMgr = null;
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
        await _pollingTimer.DisposeAsync().ConfigureAwait(false);
    }
}

