namespace burroQueue.Infrastructure.Audio;

public interface IAudioDeviceEnumerator
{
    IReadOnlyList<AudioDeviceOption> GetAvailableRenderDevices();
}
