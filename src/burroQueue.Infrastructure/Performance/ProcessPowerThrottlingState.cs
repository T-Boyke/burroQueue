using System.Runtime.InteropServices;

namespace burroQueue.Infrastructure.Performance;

[StructLayout(LayoutKind.Sequential)]
internal struct ProcessPowerThrottlingState
{
    public uint Version;
    public uint ControlMask;
    public uint StateMask;
}
