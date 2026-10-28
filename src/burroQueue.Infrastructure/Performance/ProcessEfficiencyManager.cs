using System.Diagnostics;
using System.Runtime;
using System.Runtime.InteropServices;

namespace burroQueue.Infrastructure.Performance;

public sealed class ProcessEfficiencyManager : IProcessEfficiencyManager
{
    private const int ProcessPowerThrottling = 4;
    private const int ThreadPowerThrottling = 3;
    private const uint PowerThrottlingCurrentVersion = 1;
    private const uint PowerThrottlingExecutionSpeed = 0x1;

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetProcessInformation(
        IntPtr hProcess,
        int processInformationClass,
        ref ProcessPowerThrottlingState processInformation,
        uint processInformationSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetThreadInformation(
        IntPtr hThread,
        int threadInformationClass,
        ref ProcessPowerThrottlingState threadInformation,
        uint threadInformationSize);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentThread();

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetProcessWorkingSetSize(
        IntPtr hProcess,
        IntPtr dwMinimumWorkingSetSize,
        IntPtr dwMaximumWorkingSetSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool K32EmptyWorkingSet(IntPtr hProcess);

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
                Version = PowerThrottlingCurrentVersion,
                ControlMask = PowerThrottlingExecutionSpeed,
                StateMask = PowerThrottlingExecutionSpeed
            };

            using Process currentProcess = Process.GetCurrentProcess();
            if (string.Equals(currentProcess.ProcessName, "burroQueue", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    currentProcess.PriorityClass = ProcessPriorityClass.Idle;
                    currentProcess.PriorityBoostEnabled = false;
                }
                catch
                {
                    // Fallback falls Prozess-Rechte eingeschränkt sind
                }
            }

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

    public bool EnableCurrentThreadEfficiencyMode()
    {
        if (!IsEfficiencyModeSupported)
        {
            return false;
        }

        try
        {
            ProcessPowerThrottlingState state = new()
            {
                Version = PowerThrottlingCurrentVersion,
                ControlMask = PowerThrottlingExecutionSpeed,
                StateMask = PowerThrottlingExecutionSpeed
            };

            return SetThreadInformation(
                GetCurrentThread(),
                ThreadPowerThrottling,
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
            GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
            GC.WaitForPendingFinalizers();
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                using Process currentProcess = Process.GetCurrentProcess();
                _ = SetProcessWorkingSetSize(currentProcess.Handle, -1, -1);
                _ = K32EmptyWorkingSet(currentProcess.Handle);
            }
        }
        catch
        {
            // Fail-safe: Speicher-Trimming blockiert niemals die Anwendung
        }
    }
}
