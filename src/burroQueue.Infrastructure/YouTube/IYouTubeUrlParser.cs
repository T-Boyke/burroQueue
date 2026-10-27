namespace burroQueue.Infrastructure.YouTube;

public interface IYouTubeUrlParser
{
    bool TryExtractVideoId(string rawInput, out string videoId, out Uri normalizedUri);
}

