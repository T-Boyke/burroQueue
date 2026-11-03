using System.Runtime.InteropServices;

namespace burroQueue.Infrastructure.Performance;

public sealed class MultimediaSchedulerService : IMultimediaSchedulerService
{
    private static readonly bool _isWindowsPlatform = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    public bool IsSupported => _isWindowsPlatform;

    public IDisposable RegisterPlaybackThread()
    {
        if (!_isWindowsPlatform)
        {
            return NullMultimediaSchedulerScope.Instance;
        }

        try
        {
            int taskIndex = 0;
            nint handle = AvSetMmThreadCharacteristics("Playback", ref taskIndex);
            if (handle == nint.Zero)
            {
                return NullMultimediaSchedulerScope.Instance;
            }

            return new MultimediaSchedulerScope(handle);
        }
        catch
        {
            return NullMultimediaSchedulerScope.Instance;
        }
    }

    [DllImport("avrt.dll", EntryPoint = "AvSetMmThreadCharacteristicsW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint AvSetMmThreadCharacteristics(string taskName, ref int taskIndex);
}
