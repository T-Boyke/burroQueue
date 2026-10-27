namespace burroQueue.Infrastructure.Audio;

public sealed class AudioOutputController : IAudioOutputController
{
    private double _currentVolume = 0.8;
    private bool _isDucked;

    public double CurrentVolume => _currentVolume;

    public bool IsDucked => _isDucked;

    public event EventHandler<double>? VolumeChanged;

    public void SetTargetVolume(double targetVolume, bool isDucked)
    {
        _currentVolume = Math.Clamp(targetVolume, 0.0, 1.0);
        _isDucked = isDucked;
        VolumeChanged?.Invoke(this, _currentVolume);
    }
}

