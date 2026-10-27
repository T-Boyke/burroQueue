using System.Windows;
using burroQueue.Presentation.ViewModels;
using Wpf.Ui.Controls;

namespace burroQueue.Presentation.Views;

public partial class MainWindow : FluentWindow
{
    private readonly MainWindowViewModel _viewModel;

    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.Text) || e.Data.GetDataPresent(DataFormats.UnicodeText))
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        string? droppedText = null;

        if (e.Data.GetDataPresent(DataFormats.UnicodeText))
        {
            droppedText = e.Data.GetData(DataFormats.UnicodeText) as string;
        }
        else if (e.Data.GetDataPresent(DataFormats.Text))
        {
            droppedText = e.Data.GetData(DataFormats.Text) as string;
        }

        if (!string.IsNullOrWhiteSpace(droppedText))
        {
            _viewModel.EnqueueRawUrlCommand.Execute(droppedText);
            e.Handled = true;
        }
    }
}
