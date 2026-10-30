namespace burroQueue.Domain.Model;

public sealed class FavoriteTrackEntry
{
    private readonly List<string> _likedBy;

    public string VideoId { get; }

    public Uri Url { get; }

    public string Title { get; private set; }

    public IReadOnlyList<string> LikedBy => _likedBy;

    public int Likes => _likedBy.Count;

    public string ThumbnailUrl => $"https://img.youtube.com/vi/{VideoId}/hqdefault.jpg";

    public FavoriteTrackEntry(string videoId, Uri url, string title, IEnumerable<string>? likedBy = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(videoId);
        ArgumentNullException.ThrowIfNull(url);

        VideoId = videoId.Trim();
        Url = url;
        Title = string.IsNullOrWhiteSpace(title) ? VideoId : title.Trim();
        _likedBy = [];

        if (likedBy is not null)
        {
            foreach (string user in likedBy)
            {
                if (!string.IsNullOrWhiteSpace(user))
                {
                    string normalized = user.Trim();
                    if (!_likedBy.Contains(normalized, StringComparer.OrdinalIgnoreCase))
                    {
                        _likedBy.Add(normalized);
                    }
                }
            }
        }
    }

    public bool IsLikedBy(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return false;
        }

        return _likedBy.Contains(username.Trim(), StringComparer.OrdinalIgnoreCase);
    }

    public bool ToggleVote(string username)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        string normalized = username.Trim();

        int existingIndex = _likedBy.FindIndex(u => string.Equals(u, normalized, StringComparison.OrdinalIgnoreCase));
        if (existingIndex >= 0)
        {
            _likedBy.RemoveAt(existingIndex);
            return false;
        }

        _likedBy.Add(normalized);
        return true;
    }

    public void SetVote(string username, bool isUpvoted)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return;
        }

        string normalized = username.Trim();
        int existingIndex = _likedBy.FindIndex(u => string.Equals(u, normalized, StringComparison.OrdinalIgnoreCase));
        if (isUpvoted)
        {
            if (existingIndex < 0)
            {
                _likedBy.Add(normalized);
            }
        }
        else if (existingIndex >= 0)
        {
            _likedBy.RemoveAt(existingIndex);
        }
    }

    public void UpdateTitle(string title)
    {
        if (!string.IsNullOrWhiteSpace(title))
        {
            Title = title.Trim();
        }
    }
}

