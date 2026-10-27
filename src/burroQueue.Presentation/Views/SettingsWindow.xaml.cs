using burroQueue.Presentation.ViewModels;
using Wpf.Ui.Controls;

namespace burroQueue.Presentation.Views;

public partial class SettingsWindow : FluentWindow
{
    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
