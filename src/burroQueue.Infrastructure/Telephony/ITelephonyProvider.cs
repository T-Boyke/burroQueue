namespace burroQueue.Infrastructure.Telephony;

public interface ITelephonyProvider : IAsyncDisposable
{
    string ProviderId { get; }

    string DisplayName { get; }

    bool IsAvailable { get; }

    event EventHandler<TelephonyCallEventArgs>? CallStateChanged;

    Task StartAsync(CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);
}
