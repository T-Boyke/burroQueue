using System.Windows;

namespace burroQueue.Presentation.Services;

public interface IGlobalHotkeyService : IDisposable
{
    event EventHandler? PlayPausePressed;

    event EventHandler? NextTrackPressed;

    event EventHandler? BossKeyMutePressed;

    void Register(Window window);
}

