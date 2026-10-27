using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Interop;

namespace burroQueue.Presentation.Services;

[SupportedOSPlatform("windows")]
public sealed class GlobalHotkeyService : IGlobalHotkeyService
{
    private const int WmHotkey = 0x0312;
    private const int HotkeyPlayPauseId = 9001;
    private const int HotkeyNextId = 9002;

    private const uint VkMediaPlayPause = 0xB3;
    private const uint VkMediaNextTrack = 0xB0;

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private IntPtr _windowHandle;
    private HwndSource? _source;
    private bool _isDisposed;

    public event EventHandler? PlayPausePressed;

    public event EventHandler? NextTrackPressed;

    public void Register(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        _windowHandle = new WindowInteropHelper(window).EnsureHandle();
        _source = HwndSource.FromHwnd(_windowHandle);
        _source?.AddHook(HwndHook);

        // Registriere Hardware-Mediatasten
        RegisterHotKey(_windowHandle, HotkeyPlayPauseId, 0, VkMediaPlayPause);
        RegisterHotKey(_windowHandle, HotkeyNextId, 0, VkMediaNextTrack);
    }

    private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotkey)
        {
            int id = wParam.ToInt32();
            if (id == HotkeyPlayPauseId)
            {
                PlayPausePressed?.Invoke(this, EventArgs.Empty);
                handled = true;
            }
            else if (id == HotkeyNextId)
            {
                NextTrackPressed?.Invoke(this, EventArgs.Empty);
                handled = true;
            }
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        if (_windowHandle != IntPtr.Zero)
        {
            UnregisterHotKey(_windowHandle, HotkeyPlayPauseId);
            UnregisterHotKey(_windowHandle, HotkeyNextId);
            _source?.RemoveHook(HwndHook);
            _windowHandle = IntPtr.Zero;
        }
    }
}
