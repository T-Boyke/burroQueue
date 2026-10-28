using System.Diagnostics.CodeAnalysis;
using System.Windows;
using Wpf.Ui.Controls;

namespace burroQueue.Presentation.Views;

[ExcludeFromCodeCoverage]
public partial class AboutWindow : FluentWindow
{
    public AboutWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => Wpf.Ui.Appearance.SystemThemeWatcher.Watch(this);
    }

    private void OnCloseClicked(object sender, RoutedEventArgs e)
    {
        Close();
    }
}

