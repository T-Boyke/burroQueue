using burroQueue.Domain.Exceptions;

namespace burroQueue.Domain.Model;

public sealed record CollaborativePlaylistLink
{
    public Uri RawUri { get; }

    public string PlaylistId { get; }

    public string CollaborationToken { get; }

    public CollaborativePlaylistLink(string rawUrl)
    {
        if (string.IsNullOrWhiteSpace(rawUrl))
        {
            throw new InvalidTrackUrlException("Playlist URL cannot be empty.");
        }

        if (!Uri.TryCreate(rawUrl.Trim(), UriKind.Absolute, out Uri? parsedUri))
        {
            throw new InvalidTrackUrlException("Invalid URL format for collaborative playlist.");
        }

        RawUri = parsedUri;

        string query = parsedUri.Query;
        if (string.IsNullOrEmpty(query))
        {
            throw new InvalidTrackUrlException("Collaborative playlist URL must contain query parameters.");
        }

        string playlistId = string.Empty;
        string token = string.Empty;

        string[] pairs = query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries);
        foreach (string pair in pairs)
        {
            string[] kvp = pair.Split('=', 2);
            if (kvp.Length != 2)
            {
                continue;
            }

            if (string.Equals(kvp[0], "list", StringComparison.OrdinalIgnoreCase))
            {
                playlistId = kvp[1];
            }
            else if (string.Equals(kvp[0], "jct", StringComparison.OrdinalIgnoreCase))
            {
                token = kvp[1];
            }
        }

        if (string.IsNullOrWhiteSpace(playlistId))
        {
            throw new InvalidTrackUrlException("Missing 'list' parameter in collaborative playlist URL.");
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidTrackUrlException("Missing 'jct' collaboration token in playlist URL.");
        }

        PlaylistId = playlistId;
        CollaborationToken = token;
    }

    public override string ToString() => RawUri.ToString();
}

