namespace burroQueue.Infrastructure.Audio;

public interface IAudioOutputController
{
    double CurrentVolume { get; }

    bool IsDucked { get; }

    event EventHandler<double>? VolumeChanged;

    void SetTargetVolume(double targetVolume, bool isDucked);

    double ComputeLogarithmicVolumeStep(double startVolume, double targetVolume, double progress) =>
        Math.Clamp(startVolume + ((targetVolume - startVolume) * progress), 0.0, 1.0);
}

