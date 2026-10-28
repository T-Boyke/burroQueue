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
        Action? onToggleMute = null,
        Action? onOpenAbout = null);

    void ShowNotification(string title, string message, string? thumbnailUrl = null);

    void UpdateTooltip(string tooltipText);
}
