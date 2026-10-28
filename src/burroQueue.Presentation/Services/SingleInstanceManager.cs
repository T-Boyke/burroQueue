namespace burroQueue.Presentation.Services;

public sealed class SingleInstanceManager : ISingleInstanceManager
{
    private const string DefaultMutexName = @"Local\burroQueue_SingleInstance_Mutex";
    private const string DefaultEventName = @"Local\burroQueue_Show_Event";

    private readonly string _mutexName;
    private readonly string _eventName;
    private Mutex? _mutex;
    private EventWaitHandle? _eventWaitHandle;
    private CancellationTokenSource? _listeningCts;
    private bool _hasAcquired;
    private bool _isDisposed;

    public SingleInstanceManager(string? mutexName = null, string? eventName = null)
    {
        _mutexName = mutexName ?? DefaultMutexName;
        _eventName = eventName ?? DefaultEventName;
    }

    public bool TryAcquireSingleInstance()
    {
        if (_isDisposed)
        {
            return false;
        }

        try
        {
            _mutex = new Mutex(true, _mutexName, out bool createdNew);
            _hasAcquired = createdNew;
            return createdNew;
        }
        catch
        {
            return false;
        }
    }

    public void SignalExistingInstance()
    {
        try
        {
            if (EventWaitHandle.TryOpenExisting(_eventName, out EventWaitHandle? existingEvent))
            {
                using (existingEvent)
                {
                    existingEvent.Set();
                }
            }
        }
        catch
        {
            // Fail-safe
        }
    }

    public void StartListeningForSecondaryInstance(Action onShowRequested)
    {
        ArgumentNullException.ThrowIfNull(onShowRequested);

        if (_isDisposed || !_hasAcquired)
        {
            return;
        }

        try
        {
            _eventWaitHandle = new EventWaitHandle(false, EventResetMode.AutoReset, _eventName);
            _listeningCts = new CancellationTokenSource();
            CancellationToken token = _listeningCts.Token;

            Task.Run(() =>
            {
                WaitHandle[] handles = [_eventWaitHandle, token.WaitHandle];
                while (!token.IsCancellationRequested)
                {
                    int index = WaitHandle.WaitAny(handles);
                    if (index == 0 && !token.IsCancellationRequested)
                    {
                        onShowRequested();
                    }
                    else
                    {
                        break;
                    }
                }
            }, token);
        }
        catch
        {
            // Fail-safe
        }
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _isDisposed = true;

            try
            {
                _listeningCts?.Cancel();
                _listeningCts?.Dispose();
            }
            catch
            {
            }

            try
            {
                _eventWaitHandle?.Dispose();
            }
            catch
            {
            }

            if (_hasAcquired && _mutex is not null)
            {
                try
                {
                    _mutex.ReleaseMutex();
                }
                catch
                {
                }
            }

            try
            {
                _mutex?.Dispose();
            }
            catch
            {
            }
        }
    }
}
