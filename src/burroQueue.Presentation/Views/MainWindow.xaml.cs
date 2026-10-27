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
        InitializeComponent();
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
            };
        }

        Loaded += OnLoaded;
        Closing += OnClosing;
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    internal void OnLoaded(object sender, RoutedEventArgs e)
    {
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
        HandleClosing(e, Hide);
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
