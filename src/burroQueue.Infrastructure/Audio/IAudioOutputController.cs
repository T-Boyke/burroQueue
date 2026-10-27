namespace burroQueue.Infrastructure.Audio;

public interface IAudioOutputController
{
    double CurrentVolume { get; }

    bool IsDucked { get; }

    event EventHandler<double>? VolumeChanged;

    void SetTargetVolume(double targetVolume, bool isDucked);
}

