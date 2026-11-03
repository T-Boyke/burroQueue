using System.Runtime.InteropServices;

namespace burroQueue.Infrastructure.Performance;

public sealed partial class MultimediaSchedulerService : IMultimediaSchedulerService
{
    private static readonly bool IsWindowsPlatform = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    public bool IsSupported => IsWindowsPlatform;

    public IDisposable RegisterPlaybackThread()
    {
        if (!IsWindowsPlatform)
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

    [LibraryImport("avrt.dll", EntryPoint = "AvSetMmThreadCharacteristicsW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    private static partial nint AvSetMmThreadCharacteristics(string taskName, ref int taskIndex);
}

