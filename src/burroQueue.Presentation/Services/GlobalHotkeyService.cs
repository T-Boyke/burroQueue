using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Interop;

namespace burroQueue.Presentation.Services;

[SupportedOSPlatform("windows")]
public sealed class GlobalHotkeyService : IGlobalHotkeyService
{
    private const int _wmHotkey = 0x0312;
    private const int _hotkeyPlayPauseId = 9001;
    private const int _hotkeyNextId = 9002;

    private const uint _vkMediaPlayPause = 0xB3;
    private const uint _vkMediaNextTrack = 0xB0;

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
        RegisterHotKey(_windowHandle, _hotkeyPlayPauseId, 0, _vkMediaPlayPause);
        RegisterHotKey(_windowHandle, _hotkeyNextId, 0, _vkMediaNextTrack);
    }

    internal bool ProcessMessage(int msg, IntPtr wParam)
    {
        if (msg == _wmHotkey)
        {
            int id = wParam.ToInt32();
            if (id == _hotkeyPlayPauseId)
            {
                PlayPausePressed?.Invoke(this, EventArgs.Empty);
                return true;
            }

            if (id == _hotkeyNextId)
            {
                NextTrackPressed?.Invoke(this, EventArgs.Empty);
                return true;
            }
        }

        return false;
    }

    internal IntPtr WindowHandle
    {
        get => _windowHandle;
        set => _windowHandle = value;
    }

    internal IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (ProcessMessage(msg, wParam))
        {
            handled = true;
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
            UnregisterHotKey(_windowHandle, _hotkeyPlayPauseId);
            UnregisterHotKey(_windowHandle, _hotkeyNextId);
            _source?.RemoveHook(HwndHook);
            _windowHandle = IntPtr.Zero;
        }
    }
}

