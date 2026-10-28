using System.Diagnostics;
using System.Runtime.InteropServices;

namespace burroQueue.Infrastructure.Performance;

public sealed class ProcessEfficiencyManager : IProcessEfficiencyManager
{
    private const int ProcessPowerThrottling = 4;
    private const uint ProcessPowerThrottlingCurrentVersion = 1;
    private const uint ProcessPowerThrottlingExecutionSpeed = 0x1;

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetProcessInformation(
        IntPtr hProcess,
        int processInformationClass,
        ref ProcessPowerThrottlingState processInformation,
        uint processInformationSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetProcessWorkingSetSize(
        IntPtr hProcess,
        IntPtr dwMinimumWorkingSetSize,
        IntPtr dwMaximumWorkingSetSize);

    public bool IsEfficiencyModeSupported => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && Environment.OSVersion.Version.Build >= 22000;

    public bool EnableEfficiencyMode()
    {
        if (!IsEfficiencyModeSupported)
        {
            return false;
        }

        try
        {
            ProcessPowerThrottlingState state = new()
            {
                Version = ProcessPowerThrottlingCurrentVersion,
                ControlMask = ProcessPowerThrottlingExecutionSpeed,
                StateMask = ProcessPowerThrottlingExecutionSpeed
            };

            using Process currentProcess = Process.GetCurrentProcess();
            return SetProcessInformation(
                currentProcess.Handle,
                ProcessPowerThrottling,
                ref state,
                (uint)Marshal.SizeOf<ProcessPowerThrottlingState>());
        }
        catch
        {
            return false;
        }
    }

    public void TrimMemoryWorkingSet()
    {
        try
        {
            GC.Collect(2, GCCollectionMode.Aggressive, true, true);
            GC.WaitForPendingFinalizers();
            GC.Collect(2, GCCollectionMode.Aggressive, true, true);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                using Process currentProcess = Process.GetCurrentProcess();
                _ = SetProcessWorkingSetSize(currentProcess.Handle, -1, -1);
            }
        }
        catch
        {
            // Fail-safe: Speicher-Trimming blockiert niemals die Anwendung
        }
    }
}
