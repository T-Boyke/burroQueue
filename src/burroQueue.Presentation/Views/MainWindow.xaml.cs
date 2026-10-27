using System.Windows;
using burroQueue.Presentation.Services;
using burroQueue.Presentation.ViewModels;
using Wpf.Ui.Controls;

namespace burroQueue.Presentation.Views;

public partial class MainWindow : FluentWindow
{
    private readonly MainWindowViewModel _viewModel;
    private readonly IGlobalHotkeyService _hotkeyService;

    public MainWindow(MainWindowViewModel viewModel, IGlobalHotkeyService hotkeyService)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _hotkeyService = hotkeyService;
        DataContext = _viewModel;

        Loaded += OnLoaded;
        Closing += OnClosing;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _hotkeyService.Register(this);
        _hotkeyService.PlayPausePressed += async (_, _) =>
        {
            if (_viewModel.PlaybackState == Domain.Model.PlaybackState.Playing)
            {
                await _viewModel.PauseAsync();
            }
            else
            {
                await _viewModel.PlayAsync();
            }
        };

        _hotkeyService.NextTrackPressed += async (_, _) =>
        {
            await _viewModel.SkipAsync();
        };
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // Close-to-Tray Verhalten: Fenster verbergen statt beenden
        e.Cancel = true;
        Hide();
    }

    private void OnDragOver(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetDataPresent(System.Windows.DataFormats.Text) || e.Data.GetDataPresent(System.Windows.DataFormats.UnicodeText))
        {
            e.Effects = System.Windows.DragDropEffects.Copy;
            e.Handled = true;
        }
        else
        {
            e.Effects = System.Windows.DragDropEffects.None;
        }
    }

    private void OnDrop(object sender, System.Windows.DragEventArgs e)
    {
        string? droppedText = null;

        if (e.Data.GetDataPresent(System.Windows.DataFormats.UnicodeText))
        {
            droppedText = e.Data.GetData(System.Windows.DataFormats.UnicodeText) as string;
        }
        else if (e.Data.GetDataPresent(System.Windows.DataFormats.Text))
        {
            droppedText = e.Data.GetData(System.Windows.DataFormats.Text) as string;
        }

        if (!string.IsNullOrWhiteSpace(droppedText))
        {
            _viewModel.EnqueueRawUrlCommand.Execute(droppedText);
            e.Handled = true;
        }
    }
}
