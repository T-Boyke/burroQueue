using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Media.Imaging;
using Wpf.Ui.Controls;

namespace burroQueue.Presentation.Views;

[ExcludeFromCodeCoverage]
public partial class TrackToastWindow : FluentWindow
{
    public TrackToastWindow(string title, string message, string thumbnailUrl)
    {
        InitializeComponent();
        HeaderText.Text = title;
        MessageText.Text = message;

        try
        {
            if (Uri.TryCreate(thumbnailUrl, UriKind.Absolute, out Uri? uri))
            {
                BitmapImage bmp = new();
                bmp.BeginInit();
                bmp.UriSource = uri;
                bmp.DecodePixelWidth = 128;
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                CoverImage.Source = bmp;
            }
        }
        catch
        {
            // Fail-safe
        }

        Loaded += (_, _) =>
        {
            Wpf.Ui.Appearance.SystemThemeWatcher.Watch(this);
            Rect workArea = SystemParameters.WorkArea;
            Left = workArea.Right - Width - 16;
            Top = workArea.Bottom - Height - 16;
        };
    }
}
