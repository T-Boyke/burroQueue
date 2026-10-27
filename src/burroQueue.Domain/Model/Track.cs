using burroQueue.Domain.Exceptions;

namespace burroQueue.Domain.Model;

public sealed class Track
{
    public TrackId Id { get; }

    public string VideoId { get; }

    public Uri Url { get; }

    public string Title { get; }

    public string SubmittedBy { get; }

    public DateTimeOffset EnqueuedAtUtc { get; }

    public Track(string videoId, Uri url, string title, string submittedBy, DateTimeOffset enqueuedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(videoId))
        {
            throw new InvalidTrackUrlException("A valid YouTube video identifier is required.");
        }

        ArgumentNullException.ThrowIfNull(url);

        Id = TrackId.New();
        VideoId = videoId.Trim();
        Url = url;
        Title = string.IsNullOrWhiteSpace(title) ? VideoId : title.Trim();
        SubmittedBy = string.IsNullOrWhiteSpace(submittedBy) ? "Anonymous Burro" : submittedBy.Trim();
        EnqueuedAtUtc = enqueuedAtUtc;
    }
}

