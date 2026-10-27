using System.Drawing;
using System.IO;
using System.Windows.Forms;
using burroQueue.Presentation.Views;

namespace burroQueue.Presentation.Services;

public sealed class SystrayNotificationService : ISystrayNotificationService
{
    private NotifyIcon? _notifyIcon;
    private Icon? _customIcon;
    private bool _isDisposed;

    internal static Action<MainWindow> WindowRestorer { get; set; } = RestoreMainWindow;

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    internal static void RestoreMainWindow(MainWindow window)
    {
        window.Show();
        window.WindowState = System.Windows.WindowState.Normal;
        window.Activate();
    }

    public void Initialize(MainWindow mainWindow, Action onOpenSettings, Action onExitApp)
    {
        ArgumentNullException.ThrowIfNull(mainWindow);

        InitializeCore(
            onRestoreWindow: () => WindowRestorer(mainWindow),
            onOpenSettings,
            onExitApp);
    }

    internal void InitializeCore(Action onRestoreWindow, Action onOpenSettings, Action onExitApp, string? customIconPath = null)
    {
        ContextMenuStrip menu = new();
        menu.Items.Add("burroQueue anzeigen", null, (_, _) => onRestoreWindow?.Invoke());
        menu.Items.Add("Einstellungen...", null, (_, _) => onOpenSettings?.Invoke());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Beenden", null, (_, _) => onExitApp?.Invoke());

        Icon iconToUse = SystemIcons.Application;
        try
        {
            string iconPath = customIconPath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "app.ico");
            if (File.Exists(iconPath))
            {
                _customIcon = new Icon(iconPath);
                iconToUse = _customIcon;
            }
        }
        catch
        {
            iconToUse = SystemIcons.Application;
        }

        _notifyIcon = new NotifyIcon
        {
            Text = "burroQueue – Office Jukebox",
            Icon = iconToUse,
            ContextMenuStrip = menu,
            Visible = true
        };

        _notifyIcon.DoubleClick += (_, _) => onRestoreWindow?.Invoke();
    }

    internal ContextMenuStrip? ContextMenu => _notifyIcon?.ContextMenuStrip;

    internal bool IsIconVisible => _notifyIcon?.Visible ?? false;

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

        _customIcon?.Dispose();
        _customIcon = null;
    }
}
