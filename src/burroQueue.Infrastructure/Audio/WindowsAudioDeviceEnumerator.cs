using System.Management;
using System.Runtime.Versioning;

namespace burroQueue.Infrastructure.Audio;

[SupportedOSPlatform("windows")]
public sealed class WindowsAudioDeviceEnumerator : IAudioDeviceEnumerator
{
    public IReadOnlyList<AudioDeviceOption> GetAvailableRenderDevices()
    {
        return ParseAudioDevices(() =>
        {
            List<(string? Id, string? Name)> list = [];
            using ManagementObjectSearcher searcher = new("SELECT DeviceID, Name, Status FROM Win32_SoundDevice WHERE Status = 'OK'");
            foreach (ManagementBaseObject mo in searcher.Get())
            {
                list.Add((mo["DeviceID"]?.ToString(), mo["Name"]?.ToString()));
            }
            return list;
        });
    }

    internal static List<AudioDeviceOption> ParseAudioDevices(Func<IEnumerable<(string? Id, string? Name)>> getDevices)
    {
        List<AudioDeviceOption> devices = [AudioDeviceOption.SystemDefault];

        try
        {
            foreach (var (id, name) in getDevices())
            {
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
