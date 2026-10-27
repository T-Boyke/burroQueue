using System.Runtime.Versioning;
using Microsoft.Win32;

namespace burroQueue.Infrastructure.Windows;

[SupportedOSPlatform("windows")]
public sealed class WindowsAutostartService : IWindowsAutostartService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "burroQueue";

    public bool IsAutostartEnabled
    {
        get
        {
            if (!OperatingSystem.IsWindows())
            {
                return false;
            }

            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
            return key?.GetValue(AppName) is not null;
        }
    }

    public bool SetAutostart(bool enable)
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
            if (key is null)
            {
                return false;
            }

            if (enable)
            {
                string? exePath = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exePath))
                {
                    key.SetValue(AppName, $"\"{exePath}\"");
                    return true;
                }
            }
            else
            {
                key.DeleteValue(AppName, false);
                return true;
            }
        }
        catch
        {
            return false;
        }

        return false;
    }
}
