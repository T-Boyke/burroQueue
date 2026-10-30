using burroQueue.Domain.Exceptions;

namespace burroQueue.Domain.Model;

public sealed class Track
{
    private static readonly string[] _trustedAvatarHostSuffixes =
    [
        ".ggpht.com",
        ".googleusercontent.com",
        ".ytimg.com"
    ];

    public TrackId Id { get; }

    public string VideoId { get; }

    public Uri Url { get; }

    public string Title { get; private set; }

    public string SubmittedBy { get; private set; }

    public string? ArtistName { get; private set; }

    public Uri? SubmitterAvatarUrl { get; private set; }

    public bool IsSyncedFromPlaylist { get; }

    public DateTimeOffset EnqueuedAtUtc { get; }

    public string ThumbnailUrl => $"https://img.youtube.com/vi/{VideoId}/mqdefault.jpg";

    public Track(
        string videoId,
        Uri url,
        string title,
        string submittedBy,
        DateTimeOffset enqueuedAtUtc,
        string? artistName = null,
        Uri? submitterAvatarUrl = null,
        bool isSyncedFromPlaylist = false)
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
        ArtistName = string.IsNullOrWhiteSpace(artistName) ? null : artistName.Trim();
        SubmitterAvatarUrl = SanitizeAvatarUri(submitterAvatarUrl);
        IsSyncedFromPlaylist = isSyncedFromPlaylist;
        EnqueuedAtUtc = enqueuedAtUtc;
    }

    public void SetTitle(string title)
    {
        if (!string.IsNullOrWhiteSpace(title))
        {
            Title = title.Trim();
        }
    }

    public bool UpdateAttribution(string? submittedBy, Uri? submitterAvatarUrl = null, string? artistName = null)
    {
        bool changed = false;

        if (!string.IsNullOrWhiteSpace(submittedBy) &&
            !string.Equals(SubmittedBy, submittedBy.Trim(), StringComparison.Ordinal))
        {
            SubmittedBy = submittedBy.Trim();
            changed = true;
        }

        if (!string.IsNullOrWhiteSpace(artistName) &&
            !string.Equals(ArtistName, artistName.Trim(), StringComparison.Ordinal))
        {
            ArtistName = artistName.Trim();
            changed = true;
        }

        Uri? cleanAvatar = SanitizeAvatarUri(submitterAvatarUrl);
        if (cleanAvatar is not null && SubmitterAvatarUrl != cleanAvatar)
        {
            SubmitterAvatarUrl = cleanAvatar;
            changed = true;
        }

        return changed;
    }

    public static Uri? SanitizeAvatarUri(Uri? candidate)
    {
        if (candidate is null ||
            !candidate.IsAbsoluteUri ||
            !string.Equals(candidate.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string host = candidate.Host;
        foreach (string suffix in _trustedAvatarHostSuffixes)
        {
            if (host.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(host, suffix.TrimStart('.'), StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        return null;
    }
}
