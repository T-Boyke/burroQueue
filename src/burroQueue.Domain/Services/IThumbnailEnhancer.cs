using burroQueue.Domain.Model;

namespace burroQueue.Domain.Services;

public interface IThumbnailEnhancer
{
    ThumbnailBuffer Upscale4xBgra32(ThumbnailBuffer source);
}

