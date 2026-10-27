using System.Windows;

namespace burroQueue.Presentation.Services;

public interface IGlobalHotkeyService : IDisposable
{
    event EventHandler? PlayPausePressed;

    event EventHandler? NextTrackPressed;

    void Register(Window window);
}
