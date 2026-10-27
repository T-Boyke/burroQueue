namespace burroQueue.Domain.Model;

public readonly record struct AudioVolume
{
    public double NormalLevel { get; }

    public double DuckedMultiplier { get; }

    public AudioVolume(double normalLevel, double duckedMultiplier = 0.15)
    {
        NormalLevel = Math.Clamp(normalLevel, 0.0, 1.0);
        DuckedMultiplier = Math.Clamp(duckedMultiplier, 0.0, 1.0);
    }

    public double CalculateEffectiveLevel(TelephonyState telephonyState)
    {
        bool isDucked = telephonyState is TelephonyState.Ringing or TelephonyState.Connecting or TelephonyState.ActiveCall;
        return isDucked ? NormalLevel * DuckedMultiplier : NormalLevel;
    }
}

