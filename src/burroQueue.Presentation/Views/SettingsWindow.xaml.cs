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
        };
    }

    private void OnCloseClicked(object sender, System.Windows.RoutedEventArgs e)
    {
        Close();
    }
}
