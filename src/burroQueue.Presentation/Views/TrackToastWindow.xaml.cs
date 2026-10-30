using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using burroQueue.Domain.Model;
using burroQueue.Infrastructure.Imaging;
using burroQueue.Presentation.Services;
using Wpf.Ui.Controls;

namespace burroQueue.Presentation.Views;

[ExcludeFromCodeCoverage]
public partial class TrackToastWindow : FluentWindow
{
    private static readonly SimdSuperResolutionThumbnailEnhancer _superResEnhancer = new();

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
                bmp.DecodePixelWidth = 64;
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                bmp.DownloadCompleted += (_, _) => ApplySuperResolutionIfSmall(bmp);
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

    private void ApplySuperResolutionIfSmall(BitmapSource source)
    {
        try
        {
            if (source.PixelWidth <= 0 || source.PixelHeight <= 0 || source.PixelWidth > 128)
            {
                return;
            }

            FormatConvertedBitmap bgra = new(source, PixelFormats.Bgra32, null, 0);
            int stride = bgra.PixelWidth * 4;
            byte[] raw = new byte[bgra.PixelHeight * stride];
            bgra.CopyPixels(raw, stride, 0);

            ThumbnailBuffer enhanced = _superResEnhancer.Upscale4xBgra32(new ThumbnailBuffer(bgra.PixelWidth, bgra.PixelHeight, raw));
            BitmapSource upscaled = BitmapSource.Create(
                enhanced.Width,
                enhanced.Height,
                96,
                96,
                PixelFormats.Bgra32,
                null,
                enhanced.Bgra32Pixels.ToArray(),
                enhanced.Width * 4);
            upscaled.Freeze();
            CoverImage.Source = upscaled;
        }
        catch
        {
        }
    }
}
