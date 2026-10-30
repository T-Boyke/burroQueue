using System.Globalization;
using System.Text;
using burroQueue.Domain.Model;
using burroQueue.Domain.Repositories;

namespace burroQueue.Infrastructure.Persistence;

public sealed class TomlFavoriteQueueRepository : IFavoriteQueueRepository
{
    private readonly string _storageDirectory;

    public TomlFavoriteQueueRepository(string? storageDirectory = null)
    {
        _storageDirectory = string.IsNullOrWhiteSpace(storageDirectory)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "burroQueue")
            : storageDirectory;
    }

    public async Task<IReadOnlyList<FavoriteTrackEntry>> LoadFavoritesAsync(
        string groupKey,
        CancellationToken cancellationToken = default)
    {
        string filePath = GetGroupFavoritesFilePath(groupKey);
        if (!File.Exists(filePath))
        {
            return [];
        }

        try
        {
            string rawToml = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
            (_, Dictionary<string, List<Dictionary<string, string>>> tableArrays) = TomlDocumentSerializer.Parse(rawToml);
            if (!tableArrays.TryGetValue("tracks", out List<Dictionary<string, string>>? trackSections))
            {
                return [];
            }

            List<FavoriteTrackEntry> result = new(trackSections.Count);

            foreach (Dictionary<string, string> section in trackSections)
            {
                string videoId = TomlDocumentSerializer.ParseString(section, "video_id", string.Empty);
                string urlText = TomlDocumentSerializer.ParseString(section, "url", string.Empty);
                if (string.IsNullOrWhiteSpace(videoId) || !Uri.TryCreate(urlText, UriKind.Absolute, out Uri? url))
                {
                    continue;
                }

                string title = TomlDocumentSerializer.ParseString(section, "title", videoId);
                List<string> likedBy = TomlDocumentSerializer.ParseStringArray(section, "liked_by");
                if (likedBy.Count == 0)
                {
                    continue;
                }

                result.Add(new FavoriteTrackEntry(videoId, url, title, likedBy));
            }

            return result
                .OrderByDescending(f => f.Likes)
                .ThenBy(f => f.Title, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    public async Task SaveFavoritesAsync(
        string groupKey,
        IReadOnlyList<FavoriteTrackEntry> favorites,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(favorites);
        string filePath = GetGroupFavoritesFilePath(groupKey);
        string sanitizedGroup = SanitizeGroupKey(groupKey);

        StringBuilder sb = new(512);
        sb.AppendLine("# burroQueue Group Favorites Queue (TOML v1.0)");
        sb.AppendLine("[group]");
        sb.AppendLine(CultureInfo.InvariantCulture, $"id = {TomlDocumentSerializer.EscapeString(sanitizedGroup)}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"updated_utc = {TomlDocumentSerializer.EscapeString(DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture))}");

        foreach (FavoriteTrackEntry fav in favorites.Where(f => f.Likes > 0).OrderByDescending(f => f.Likes))
        {
            sb.AppendLine();
            sb.AppendLine("[[tracks]]");
            sb.AppendLine(CultureInfo.InvariantCulture, $"video_id = {TomlDocumentSerializer.EscapeString(fav.VideoId)}");
            sb.AppendLine(CultureInfo.InvariantCulture, $"url = {TomlDocumentSerializer.EscapeString(fav.Url.ToString())}");
            sb.AppendLine(CultureInfo.InvariantCulture, $"title = {TomlDocumentSerializer.EscapeString(fav.Title)}");
            sb.AppendLine(CultureInfo.InvariantCulture, $"likes = {fav.Likes.ToString(CultureInfo.InvariantCulture)}");
            sb.AppendLine(CultureInfo.InvariantCulture, $"liked_by = {TomlDocumentSerializer.FormatStringArray(fav.LikedBy)}");
        }

        await TomlDocumentSerializer.WriteAtomicallyAsync(filePath, sb.ToString(), cancellationToken).ConfigureAwait(false);
    }

    public string GetGroupFavoritesFilePath(string groupKey)
    {
        string sanitized = SanitizeGroupKey(groupKey);
        return Path.Combine(_storageDirectory, $"favorites.{sanitized}.toml");
    }

    public static string SanitizeGroupKey(string? groupKey)
    {
        if (string.IsNullOrWhiteSpace(groupKey))
        {
            return "default";
        }

        Span<char> buffer = stackalloc char[Math.Min(groupKey.Length, 64)];
        int written = 0;
        bool lastWasHyphen = false;

        foreach (char c in groupKey.AsSpan().Trim())
        {
            if (written >= buffer.Length)
            {
                break;
            }

            if (char.IsAsciiLetterOrDigit(c))
            {
                buffer[written++] = char.ToLowerInvariant(c);
                lastWasHyphen = false;
            }
            else if (!lastWasHyphen && written > 0)
            {
                buffer[written++] = '-';
                lastWasHyphen = true;
            }
        }

        while (written > 0 && buffer[written - 1] == '-')
        {
            written--;
        }

        return written == 0 ? "default" : new string(buffer[..written]);
    }
}
