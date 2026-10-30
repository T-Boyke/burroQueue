using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using burroQueue.Presentation.Services;
using Wpf.Ui.Controls;

namespace burroQueue.Presentation.Views;

[ExcludeFromCodeCoverage]
public partial class TrackToastWindow : FluentWindow
{
    public TrackToastWindow(
        string title,
        string message,
        string thumbnailUrl,
        string? submitterName = null,
        Uri? submitterAvatarUrl = null)
    {
        InitializeComponent();
        HeaderText.Text = title;
        MessageText.Text = message;

        if (!string.IsNullOrWhiteSpace(submitterName))
        {
            SubmitterText.Text = submitterName;
            SubmitterText.Visibility = Visibility.Visible;
        }

        try
        {
            if (Uri.TryCreate(thumbnailUrl, UriKind.Absolute, out Uri? uri))
            {
                BitmapImage bmp = new();
                bmp.BeginInit();
                bmp.UriSource = uri;
                bmp.DecodePixelWidth = 192;
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                CoverImage.Source = bmp;
            }
        }
        catch
        {
            // Fail-safe
        }

        Loaded += async (_, _) =>
        {
            Wpf.Ui.Appearance.ApplicationAccentColorManager.ApplySystemAccent();
            Wpf.Ui.Appearance.SystemThemeWatcher.Watch(this, WindowBackdropType.Mica, true);
            Rect workArea = SystemParameters.WorkArea;
            Left = workArea.Right - Width - 16;
            Top = workArea.Bottom - Height - 16;

            if (submitterAvatarUrl is not null)
            {
                ImageSource? avatarSource = await AvatarBitmapCacheService.Shared
                    .GetOrLoadAsync(submitterAvatarUrl)
                    .ConfigureAwait(true);

                if (avatarSource is not null && IsLoaded)
                {
                    CollaboratorAvatarBrush.ImageSource = avatarSource;
                    CollaboratorAvatarBadge.Visibility = Visibility.Visible;
                }
            }
        };
    }
}
