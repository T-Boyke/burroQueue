using burroQueue.Domain.Model;

namespace burroQueue.Infrastructure.Telephony;

public sealed class NullTelephonyProvider : ITelephonyProvider
{
    public string ProviderId => "None";

    public string DisplayName => "Deaktiviert (Kein Auto-Ducking)";

    public bool IsAvailable => true;

    public event EventHandler<TelephonyCallEventArgs>? CallStateChanged;

    public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
