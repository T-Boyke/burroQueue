namespace burroQueue.Domain.Model;

public readonly record struct MusicValidationResult(
    bool IsVerifiedMusic,
    string VideoId,
    string Title,
    string Author,
    TimeSpan Duration,
    string? RejectionReason);

