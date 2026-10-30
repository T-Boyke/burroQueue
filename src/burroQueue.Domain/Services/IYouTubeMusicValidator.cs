using burroQueue.Domain.Model;

namespace burroQueue.Domain.Services;

public interface IYouTubeMusicValidator
{
    Task<MusicValidationResult> ValidateAsync(
        string videoId,
        TimeSpan maxDuration,
        CancellationToken cancellationToken = default);
}
