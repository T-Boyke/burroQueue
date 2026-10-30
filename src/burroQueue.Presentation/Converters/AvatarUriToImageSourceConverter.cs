using System.Globalization;
using System.Windows.Data;
using burroQueue.Presentation.Services;

namespace burroQueue.Presentation.Converters;

public sealed class AvatarUriToImageSourceConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        Uri? uri = value as Uri;
        if (uri is null && value is string raw && !string.IsNullOrWhiteSpace(raw))
        {
            _ = Uri.TryCreate(raw, UriKind.Absolute, out uri);
        }

        return AvatarBitmapCacheService.Shared.GetOrCreateUriBitmap(uri);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        System.Windows.Data.Binding.DoNothing;
}
