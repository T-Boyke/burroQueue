using burroQueue.Domain.Model;

namespace burroQueue.Infrastructure.Telephony;

public sealed class TelephonyCallEventArgs : EventArgs
{
    public string ProviderId { get; }

    public TelephonyState State { get; }

    public bool IsCallActive { get; }

    public TelephonyCallEventArgs(string providerId, TelephonyState state)
    {
        ProviderId = providerId;
        State = state;
        IsCallActive = state is TelephonyState.Ringing or TelephonyState.Connecting or TelephonyState.ActiveCall;
    }
}
