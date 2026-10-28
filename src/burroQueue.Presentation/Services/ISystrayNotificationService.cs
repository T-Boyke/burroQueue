using burroQueue.Presentation.Views;

namespace burroQueue.Presentation.Services;

public interface ISystrayNotificationService : IDisposable
{
    void Initialize(
        MainWindow mainWindow,
        Action onOpenSettings,
        Action onExitApp,
        Action? onTogglePlayPause = null,
        Action? onSkipNext = null,
        Action? onToggleMute = null);

    void ShowNotification(string title, string message);

    void UpdateTooltip(string tooltipText);
}


