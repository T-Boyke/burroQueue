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

    public double ComputeLogarithmicVolumeStep(double startVolume, double targetVolume, double progress)
    {
        double t = Math.Clamp(progress, 0.0, 1.0);
        // Perceptually smooth logarithmic/exponential curve (human hearing dB response)
        double curvedT = 1.0 - Math.Pow(1.0 - t, 2.2);
        return Math.Clamp(startVolume + ((targetVolume - startVolume) * curvedT), 0.0, 1.0);
    }
}

