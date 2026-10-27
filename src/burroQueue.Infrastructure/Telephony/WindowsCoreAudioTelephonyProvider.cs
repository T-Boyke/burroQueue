using burroQueue.Domain.Model;

namespace burroQueue.Infrastructure.Telephony;

public sealed class WindowsCoreAudioTelephonyProvider : ITelephonyProvider
{
    public string ProviderId => "WindowsAudio";

    public string DisplayName => "Windows Core Audio (Kommunikationserkennung)";

    public bool IsAvailable => OperatingSystem.IsWindows();

    public event EventHandler<TelephonyCallEventArgs>? CallStateChanged;

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public void TriggerStateManually(TelephonyState state)
    {
        CallStateChanged?.Invoke(this, new TelephonyCallEventArgs(ProviderId, state));
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

