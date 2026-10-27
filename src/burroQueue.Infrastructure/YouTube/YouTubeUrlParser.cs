using System.Text.RegularExpressions;

namespace burroQueue.Infrastructure.YouTube;

public sealed partial class YouTubeUrlParser : IYouTubeUrlParser
{
    [GeneratedRegex(@"(?:youtube\.com/(?:watch\?.*v=|shorts/|embed/|live/|v/)|youtu\.be/)([A-Za-z0-9_-]{11})|^([A-Za-z0-9_-]{11})$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex VideoIdPattern();

    public bool TryExtractVideoId(string rawInput, out string videoId, out Uri normalizedUri)
    {
        videoId = string.Empty;
        normalizedUri = null!;

        if (string.IsNullOrWhiteSpace(rawInput))
        {
            return false;
        }

        Match match = VideoIdPattern().Match(rawInput.Trim());
        if (!match.Success)
        {
            return false;
        }

        videoId = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
        normalizedUri = new Uri($"https://www.youtube.com/watch?v={videoId}");
        return true;
    }
}

