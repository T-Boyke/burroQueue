using System.Management;
using System.Runtime.Versioning;

namespace burroQueue.Infrastructure.Audio;

[SupportedOSPlatform("windows")]
public sealed class WindowsAudioDeviceEnumerator : IAudioDeviceEnumerator
{
    public IReadOnlyList<AudioDeviceOption> GetAvailableRenderDevices()
    {
        List<AudioDeviceOption> devices = [AudioDeviceOption.SystemDefault];

        if (!OperatingSystem.IsWindows())
        {
            return devices;
        }

        try
        {
            using ManagementObjectSearcher searcher = new("SELECT DeviceID, Name, Status FROM Win32_SoundDevice WHERE Status = 'OK'");
            foreach (ManagementBaseObject mo in searcher.Get())
            {
                string? name = mo["Name"]?.ToString();
                string? id = mo["DeviceID"]?.ToString();

                if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(id))
                {
                    devices.Add(new AudioDeviceOption(id, name, false));
                }
            }
        }
        catch
        {
            // Fallback auf Standardgerät bei WMI-Einschränkungen
        }

        return devices;
    }
}
