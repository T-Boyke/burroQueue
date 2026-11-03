using System.Runtime.InteropServices;

namespace burroQueue.Infrastructure.Performance;

public sealed class MultimediaSchedulerScope(nint handle) : IDisposable
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

    [DllImport("avrt.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AvRevertMmThreadCharacteristics(nint avrtHandle);
}
