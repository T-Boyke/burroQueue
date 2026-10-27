using System.Runtime.Versioning;
using Microsoft.Win32;

namespace burroQueue.Infrastructure.Windows;

[SupportedOSPlatform("windows")]
public sealed class WindowsAutostartService : IWindowsAutostartService
{
    private const string _runKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string _appName = "burroQueue";

    public bool IsAutostartEnabled
    {
        get
        {
            if (!OperatingSystem.IsWindows())
            {
                return false;
            }

            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(_runKeyPath, false);
            return key?.GetValue(_appName) is not null;
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
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(_runKeyPath, true);
            if (key is null)
            {
                return false;
            }

            if (enable)
            {
                string? exePath = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exePath))
                {
                    key.SetValue(_appName, $"\"{exePath}\"");
                    return true;
                }
            }
            else
            {
                key.DeleteValue(_appName, false);
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

