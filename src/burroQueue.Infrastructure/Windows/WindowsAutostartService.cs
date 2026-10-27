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
            return EvaluateAutostart(() => Registry.CurrentUser.OpenSubKey(_runKeyPath, false));
        }
    }

    internal static bool EvaluateAutostart(Func<RegistryKey?> openKey)
    {
        try
        {
            using RegistryKey? key = openKey();
            return key?.GetValue(_appName) is not null;
        }
        catch
        {
            return false;
        }
    }

    public bool SetAutostart(bool enable)
    {
        return ApplyAutostart(enable, () => Registry.CurrentUser.OpenSubKey(_runKeyPath, true), Environment.ProcessPath);
    }

    internal static bool ApplyAutostart(bool enable, Func<RegistryKey?> openWritableKey, string? exePath)
    {
        try
        {
            using RegistryKey? key = openWritableKey();
            if (key is null)
            {
                return false;
            }

            if (enable)
            {
                if (!string.IsNullOrEmpty(exePath))
                {
                    key.SetValue(_appName, $"\"{exePath}\"");
                    return true;
                }

                return false;
            }

            key.DeleteValue(_appName, false);
            return true;
        }
        catch
        {
            return false;
        }
    }
}

