using burroQueue.Presentation.Views;

namespace burroQueue.Presentation.Services;

public interface ISystrayNotificationService : IDisposable
{
    void Initialize(MainWindow mainWindow, Action onOpenSettings, Action onExitApp);

    void ShowNotification(string title, string message);
}

