using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace burroQueue.Presentation.Services;

[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
public sealed class Win32ClipboardMonitorService : IClipboardMonitorService
{
    private const int WmClipboardUpdate = 0x031D;

    private HwndSource? _hwndSource;
    private IntPtr _attachedHwnd;
    private string _lastObservedText = string.Empty;
    private bool _isAttached;

    public event EventHandler<string>? ClipboardTextChanged;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AddClipboardFormatListener(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

    public void AttachToWindow(IntPtr windowHandle)
    {
        if (windowHandle == IntPtr.Zero || _isAttached)
        {
            return;
        }

        _hwndSource = HwndSource.FromHwnd(windowHandle);
        if (_hwndSource is null)
        {
            return;
        }

        _attachedHwnd = windowHandle;
        _hwndSource.AddHook(WndProc);
        _isAttached = AddClipboardFormatListener(windowHandle);
    }

    public void Detach()
    {
        if (_hwndSource is not null)
        {
            _hwndSource.RemoveHook(WndProc);
            _hwndSource = null;
        }

        if (_isAttached && _attachedHwnd != IntPtr.Zero)
        {
            _ = RemoveClipboardFormatListener(_attachedHwnd);
            _isAttached = false;
            _attachedHwnd = IntPtr.Zero;
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmClipboardUpdate)
        {
            try
            {
                if (System.Windows.Clipboard.ContainsText())
                {
                    string text = System.Windows.Clipboard.GetText()?.Trim() ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(text) &&
                        !string.Equals(text, _lastObservedText, StringComparison.Ordinal))
                    {
                        _lastObservedText = text;
                        ClipboardTextChanged?.Invoke(this, text);
                    }
                }
            }
            catch
            {
                // Ignore transient OpenClipboard lock contentions from other processes
            }
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        Detach();
    }
}
