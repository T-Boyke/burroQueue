using System.Diagnostics.CodeAnalysis;
using burroQueue.Presentation.ViewModels;
using Wpf.Ui.Controls;

namespace burroQueue.Presentation.Views;

[ExcludeFromCodeCoverage]
public partial class SettingsWindow : FluentWindow
{
    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += (_, _) =>
        {
            WindowBackdropType backdrop = MainWindow.ParseBackdropType(viewModel.SelectedWindowBackdrop);
            WindowBackdropType = backdrop;
            if (viewModel.UseSystemAccentColor)
            {
                Wpf.Ui.Appearance.ApplicationAccentColorManager.ApplySystemAccent();
            }

            Wpf.Ui.Appearance.SystemThemeWatcher.Watch(this, backdrop, viewModel.UseSystemAccentColor);
            _ = viewModel.ScanGroupsAsync();
        };
    }

    private void OnScrollViewerPreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
    {
        if (sender is System.Windows.Controls.ScrollViewer scrollViewer && !e.Handled)
        {
            scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - e.Delta);
            e.Handled = true;
        }
    }

    private void OnCloseClicked(object sender, System.Windows.RoutedEventArgs e)
    {
        Close();
    }
}
