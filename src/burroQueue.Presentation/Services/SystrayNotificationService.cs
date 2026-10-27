using System.Windows.Forms;
using burroQueue.Presentation.Views;

namespace burroQueue.Presentation.Services;

public sealed class SystrayNotificationService : ISystrayNotificationService
{
    private NotifyIcon? _notifyIcon;
    private bool _isDisposed;

    public void Initialize(MainWindow mainWindow, Action onOpenSettings, Action onExitApp)
    {
        ArgumentNullException.ThrowIfNull(mainWindow);

        ContextMenuStrip menu = new();
        menu.Items.Add("🫏 burroQueue anzeigen", null, (_, _) =>
        {
            mainWindow.Show();
            mainWindow.WindowState = System.Windows.WindowState.Normal;
            mainWindow.Activate();
        });

        menu.Items.Add("⚙️ Einstellungen", null, (_, _) => onOpenSettings?.Invoke());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("❌ Beenden", null, (_, _) => onExitApp?.Invoke());

        _notifyIcon = new NotifyIcon
        {
            Text = "burroQueue – Office Jukebox",
            Icon = System.Drawing.SystemIcons.Application,
            ContextMenuStrip = menu,
            Visible = true
        };

        _notifyIcon.DoubleClick += (_, _) =>
        {
            mainWindow.Show();
            mainWindow.WindowState = System.Windows.WindowState.Normal;
            mainWindow.Activate();
        };
    }

    public void ShowNotification(string title, string message)
    {
        _notifyIcon?.ShowBalloonTip(3000, title, message, ToolTipIcon.Info);
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        if (_notifyIcon is not null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }
    }
}

