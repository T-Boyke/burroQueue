using burroQueue.Domain.Model;

namespace burroQueue.Infrastructure.Telephony;

public interface ITelephonyManager : IAsyncDisposable
{
    IReadOnlyList<ITelephonyProvider> AvailableProviders { get; }

    ITelephonyProvider ActiveProvider { get; }

    TelephonyState CurrentState { get; }

    event EventHandler<TelephonyCallEventArgs>? StateChanged;

    Task SwitchProviderAsync(string providerId, CancellationToken cancellationToken = default);

    Task StartAsync(CancellationToken cancellationToken = default);
}
