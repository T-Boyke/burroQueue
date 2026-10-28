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

    public void Initialize(
        MainWindow mainWindow,
        Action onOpenSettings,
        Action onExitApp,
        Action? onTogglePlayPause = null,
        Action? onSkipNext = null,
        Action? onToggleMute = null)
    {
        ArgumentNullException.ThrowIfNull(mainWindow);

        InitializeCore(
            onRestoreWindow: () => WindowRestorer(mainWindow),
            onOpenSettings,
            onExitApp,
            onTogglePlayPause,
            onSkipNext,
            onToggleMute);
    }

    internal void InitializeCore(
        Action onRestoreWindow,
        Action onOpenSettings,
        Action onExitApp,
        Action? onTogglePlayPause = null,
        Action? onSkipNext = null,
        Action? onToggleMute = null,
        string? customIconPath = null)
    {
        ContextMenuStrip menu = new();
        menu.Items.Add("burroQueue anzeigen", null, (_, _) => onRestoreWindow?.Invoke());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Wiedergabe / Pause", null, (_, _) => onTogglePlayPause?.Invoke());
        menu.Items.Add("Nächster Titel", null, (_, _) => onSkipNext?.Invoke());
        menu.Items.Add("Stummschalten", null, (_, _) => onToggleMute?.Invoke());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Einstellungen...", null, (_, _) => onOpenSettings?.Invoke());
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
            else if (!string.IsNullOrEmpty(Environment.ProcessPath) && File.Exists(Environment.ProcessPath))
            {
                _customIcon = Icon.ExtractAssociatedIcon(Environment.ProcessPath);
                if (_customIcon is not null)
                {
                    iconToUse = _customIcon;
                }
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

    internal string NotifyIconText => _notifyIcon?.Text ?? string.Empty;

    public void UpdateTooltip(string tooltipText)
    {
        if (_notifyIcon is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(tooltipText))
        {
            _notifyIcon.Text = "burroQueue – Office Jukebox";
            return;
        }

        string text = tooltipText.Trim();
        if (text.Length > 63)
        {
            text = text[..60] + "...";
        }

        _notifyIcon.Text = text;
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

        _customIcon?.Dispose();
        _customIcon = null;
    }
}
