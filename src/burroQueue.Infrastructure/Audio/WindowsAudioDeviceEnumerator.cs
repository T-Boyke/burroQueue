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
            try
            {
                // Windows MMDEVAPI: {0.0.0.00000000} steht für eRender (Wiedergabe-Endpoints)
                using ManagementObjectSearcher searcher = new(
                    "SELECT DeviceID, Name FROM Win32_PnPEntity WHERE PNPClass = 'AudioEndpoint' AND Status = 'OK' AND DeviceID LIKE '%{0.0.0.00000000}%'");
                foreach (ManagementBaseObject mo in searcher.Get())
                {
                    list.Add((mo["DeviceID"]?.ToString(), mo["Name"]?.ToString()));
                }
            }
            catch
            {
                // Fallback auf Win32_SoundDevice bei WMI-Einschränkungen
                using ManagementObjectSearcher fallbackSearcher = new("SELECT DeviceID, Name FROM Win32_SoundDevice WHERE Status = 'OK'");
                foreach (ManagementBaseObject mo in fallbackSearcher.Get())
                {
                    list.Add((mo["DeviceID"]?.ToString(), mo["Name"]?.ToString()));
                }
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
