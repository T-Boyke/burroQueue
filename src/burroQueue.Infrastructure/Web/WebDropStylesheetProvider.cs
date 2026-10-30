using System.Reflection;
using System.Text;

namespace burroQueue.Infrastructure.Web;

public static class WebDropStylesheetProvider
{
    private const string ResourceName = "burroQueue.Infrastructure.Web.Assets.webdrop.uno.min.css";

    private static readonly string _cachedCss = LoadEmbeddedCss();
    private static readonly byte[] _cachedCssUtf8Bytes = Encoding.UTF8.GetBytes(_cachedCss);

    public static string GetCss() => _cachedCss;

    public static ReadOnlyMemory<byte> GetCssUtf8Bytes() => _cachedCssUtf8Bytes;

    private static string LoadEmbeddedCss()
    {
        Assembly assembly = typeof(WebDropStylesheetProvider).Assembly;
        using Stream stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Missing embedded UnoCSS stylesheet resource '{ResourceName}'.");
        using StreamReader reader = new(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
