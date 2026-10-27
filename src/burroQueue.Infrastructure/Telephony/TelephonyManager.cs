using burroQueue.Domain.Model;

namespace burroQueue.Infrastructure.Telephony;

public sealed class TelephonyManager : ITelephonyManager
{
    private readonly List<ITelephonyProvider> _providers;
    private ITelephonyProvider _activeProvider;
    private TelephonyState _currentState = TelephonyState.Idle;

    public IReadOnlyList<ITelephonyProvider> AvailableProviders => _providers.AsReadOnly();

    public ITelephonyProvider ActiveProvider => _activeProvider;

    public TelephonyState CurrentState => _currentState;

    public event EventHandler<TelephonyCallEventArgs>? StateChanged;

    public TelephonyManager(IEnumerable<ITelephonyProvider> providers, string defaultProviderId = "SwyxIt")
    {
        _providers = providers.ToList();
        if (_providers.Count == 0)
        {
            _providers.Add(new NullTelephonyProvider());
        }

        _activeProvider = _providers.FirstOrDefault(p => string.Equals(p.ProviderId, defaultProviderId, StringComparison.OrdinalIgnoreCase))
                          ?? _providers[0];

        foreach (ITelephonyProvider provider in _providers)
        {
            provider.CallStateChanged += OnProviderCallStateChanged;
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await _activeProvider.StartAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task SwitchProviderAsync(string providerId, CancellationToken cancellationToken = default)
    {
        ITelephonyProvider? target = _providers.FirstOrDefault(p => string.Equals(p.ProviderId, providerId, StringComparison.OrdinalIgnoreCase));
        if (target is null || target == _activeProvider)
        {
            return;
        }

        await _activeProvider.StopAsync(cancellationToken).ConfigureAwait(false);
        _activeProvider = target;
        await _activeProvider.StartAsync(cancellationToken).ConfigureAwait(false);

        _currentState = TelephonyState.Idle;
        StateChanged?.Invoke(this, new TelephonyCallEventArgs(_activeProvider.ProviderId, _currentState));
    }

    private void OnProviderCallStateChanged(object? sender, TelephonyCallEventArgs e)
    {
        if (sender is ITelephonyProvider provider && provider == _activeProvider)
        {
            _currentState = e.State;
            StateChanged?.Invoke(this, e);
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (ITelephonyProvider provider in _providers)
        {
            provider.CallStateChanged -= OnProviderCallStateChanged;
            await provider.DisposeAsync().ConfigureAwait(false);
        }
    }
}

