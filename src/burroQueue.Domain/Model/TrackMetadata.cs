namespace burroQueue.Domain.Model;

public sealed record TrackMetadata(string Title, string AuthorName, Uri? ThumbnailUri)
{
    public static TrackMetadata Unknown(string fallbackTitle) =>
        new(string.IsNullOrWhiteSpace(fallbackTitle) ? "Unknown Track" : fallbackTitle.Trim(), "Unknown Artist", null);
}
