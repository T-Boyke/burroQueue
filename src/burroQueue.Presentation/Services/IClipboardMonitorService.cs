namespace burroQueue.Presentation.Services;

public interface IClipboardMonitorService : IDisposable
{
    event EventHandler<string>? ClipboardTextChanged;

    void AttachToWindow(IntPtr windowHandle);

    void Detach();
}

