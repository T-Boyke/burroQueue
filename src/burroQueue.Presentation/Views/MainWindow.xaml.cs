using System.ComponentModel;
using System.Windows;
using burroQueue.Presentation.Services;
using burroQueue.Presentation.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Wpf.Ui.Controls;
using DataFormats = System.Windows.DataFormats;
using DragDropEffects = System.Windows.DragDropEffects;
using DragEventArgs = System.Windows.DragEventArgs;
using IDataObject = System.Windows.IDataObject;

namespace burroQueue.Presentation.Views;

[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
public partial class MainWindow : FluentWindow
{
    private readonly MainWindowViewModel _viewModel;
    private readonly IGlobalHotkeyService _hotkeyService;

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    public MainWindow(MainWindowViewModel viewModel, IGlobalHotkeyService hotkeyService, IServiceProvider? serviceProvider = null)
    {
        App.LogStartup("MainWindow constructor starting InitializeComponent...");
        InitializeComponent();
        App.LogStartup("MainWindow InitializeComponent finished");
        _viewModel = viewModel;
        _hotkeyService = hotkeyService;
        DataContext = _viewModel;

        if (serviceProvider is not null)
        {
            _viewModel.OpenSettingsRequested += (_, _) =>
            {
                SettingsWindow settingsWindow = serviceProvider.GetRequiredService<SettingsWindow>();
                settingsWindow.Owner = this;
                settingsWindow.ShowDialog();
                _viewModel.ReloadSettings();
            };
        }

        Loaded += (_, _) => App.LogStartup($"MainWindow Loaded event fired. HWND=0x{new System.Windows.Interop.WindowInteropHelper(this).Handle:X}");
        Activated += (_, _) => App.LogStartup("MainWindow Activated event fired");
        Deactivated += (_, _) => App.LogStartup("MainWindow Deactivated event fired");
        IsVisibleChanged += (_, _) => App.LogStartup($"MainWindow IsVisibleChanged event fired: {IsVisible}");
        StateChanged += (_, _) => App.LogStartup($"MainWindow StateChanged event fired: {WindowState}");
        Closed += (_, _) => App.LogStartup("MainWindow Closed event fired");

        Loaded += OnLoaded;
        Closing += OnClosing;
        KeyDown += OnKeyDown;
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    internal void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        bool isCtrl = (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) == System.Windows.Input.ModifierKeys.Control;
        HandleKeyDown(_viewModel, e.Key, isCtrl, () => System.Windows.Clipboard.ContainsText() ? System.Windows.Clipboard.GetText() : null, h => e.Handled = h);
    }

    internal static void HandleKeyDown(MainWindowViewModel viewModel, System.Windows.Input.Key key, bool isControl, Func<string?> getClipboardText, Action<bool> setHandled)
    {
        if (key == System.Windows.Input.Key.V && isControl)
        {
            string? clipboardText = getClipboardText();
            if (!string.IsNullOrWhiteSpace(clipboardText))
            {
                viewModel.EnqueueRawUrlCommand.Execute(clipboardText.Trim());
                setHandled(true);
            }
        }
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    internal void OnQuickAddKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        HandleQuickAddKeyDown(_viewModel, e.Key, h => e.Handled = h);
    }

    internal static void HandleQuickAddKeyDown(MainWindowViewModel viewModel, System.Windows.Input.Key key, Action<bool> setHandled)
    {
        if (key == System.Windows.Input.Key.Enter)
        {
            viewModel.SubmitQuickAddCommand.Execute(null);
            setHandled(true);
        }
    }


    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    internal void OnLoaded(object sender, RoutedEventArgs e)
    {
        Wpf.Ui.Appearance.SystemThemeWatcher.Watch(this);
        HandleHotkeyRegistration(_hotkeyService, this, _viewModel);
    }

    internal static void HandleHotkeyRegistration(IGlobalHotkeyService hotkeyService, Window window, MainWindowViewModel viewModel)
    {
        hotkeyService.Register(window);
        hotkeyService.PlayPausePressed += async (_, _) =>
        {
            if (viewModel.PlaybackState == Domain.Model.PlaybackState.Playing)
            {
                await viewModel.PauseAsync();
            }
            else
            {
                await viewModel.PlayAsync();
            }
        };

        hotkeyService.NextTrackPressed += async (_, _) =>
        {
            await viewModel.SkipAsync();
        };
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    internal void OnClosing(object? sender, CancelEventArgs e)
    {
        App.LogStartup($"MainWindow OnClosing invoked! Cancel={e.Cancel}");
        HandleClosing(e, Hide);
        App.LogStartup($"MainWindow OnClosing finished. Cancel={e.Cancel}, IsVisible={IsVisible}");
    }

    internal static void HandleClosing(CancelEventArgs e, Action hideAction)
    {
        e.Cancel = true;
        hideAction();
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    internal void OnDragOver(object sender, DragEventArgs e)
    {
        HandleDragOver(e.Data, eff => e.Effects = eff, h => e.Handled = h);
    }

    internal static void HandleDragOver(IDataObject data, Action<DragDropEffects> setEffects, Action<bool> setHandled)
    {
        if (data.GetDataPresent(DataFormats.Text) || data.GetDataPresent(DataFormats.UnicodeText))
        {
            setEffects(DragDropEffects.Copy);
            setHandled(true);
        }
        else
        {
            setEffects(DragDropEffects.None);
        }
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    internal void OnDrop(object sender, DragEventArgs e)
    {
        HandleDrop(_viewModel, e.Data, h => e.Handled = h);
    }

    internal static void HandleDrop(MainWindowViewModel viewModel, IDataObject data, Action<bool> setHandled)
    {
        string? droppedText = null;

        if (data.GetDataPresent(DataFormats.UnicodeText))
        {
            droppedText = data.GetData(DataFormats.UnicodeText) as string;
        }
        else if (data.GetDataPresent(DataFormats.Text))
        {
            droppedText = data.GetData(DataFormats.Text) as string;
        }

        if (!string.IsNullOrWhiteSpace(droppedText))
        {
            viewModel.EnqueueRawUrlCommand.Execute(droppedText);
            setHandled(true);
        }
    }
}
