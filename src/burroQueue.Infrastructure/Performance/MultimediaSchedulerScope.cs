using System.Runtime.InteropServices;

namespace burroQueue.Infrastructure.Performance;

public sealed partial class MultimediaSchedulerScope(nint handle) : IDisposable
{
    private nint _handle = handle;

    public void Dispose()
    {
        nint h = Interlocked.Exchange(ref _handle, nint.Zero);
        if (h != nint.Zero)
        {
            _ = AvRevertMmThreadCharacteristics(h);
        }
    }

    [LibraryImport("avrt.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AvRevertMmThreadCharacteristics(nint avrtHandle);
}
