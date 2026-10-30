using System.Text.Json;
using burroQueue.Domain.Model;

namespace burroQueue.Infrastructure.YouTube;

public static class InnerTubePlaylistParser
{
    private static readonly string[] _addedByPrefixes =
    [
        "Added by ",
        "Hinzugefügt von ",
        "Contributed by ",
        "Von "
    ];

    public static InnerTubePlaylistParseResult ParseBrowseResponse(
        string json,
        string? inheritedOwnerName = null,
        Uri? inheritedOwnerAvatar = null)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new InnerTubePlaylistParseResult([], null, inheritedOwnerName, inheritedOwnerAvatar);
        }

        try
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;

            (string? ownerName, Uri? ownerAvatar) = ExtractPlaylistOwner(root);
            ownerName ??= inheritedOwnerName;
            ownerAvatar ??= inheritedOwnerAvatar;

            List<InnerTubePlaylistTrackInfo> rawItems = [];
            string? continuationToken = null;
            bool hasAnyExplicitContributor = false;

            TraverseRenderers(root, rawItems, ref continuationToken, ref hasAnyExplicitContributor);

            List<Track> tracks = new(rawItems.Count);
            DateTimeOffset now = DateTimeOffset.UtcNow;

            foreach (InnerTubePlaylistTrackInfo item in rawItems)
            {
                string? submitter = item.ContributorName;
                Uri? avatar = item.ContributorAvatar;

                if (string.IsNullOrWhiteSpace(submitter))
                {
                    if ((hasAnyExplicitContributor || ownerAvatar is not null) && !string.IsNullOrWhiteSpace(ownerName))
                    {
                        submitter = ownerName;
                        avatar ??= ownerAvatar;
                    }
                    else
                    {
                        submitter = ownerName ?? item.ArtistName ?? "YouTube Playlist";
                        avatar ??= ownerAvatar;
                    }
                }

                Uri videoUri = new($"https://www.youtube.com/watch?v={item.VideoId}");
                tracks.Add(new Track(
                    item.VideoId,
                    videoUri,
                    item.Title,
                    submitter,
                    now,
                    artistName: item.ArtistName,
                    submitterAvatarUrl: avatar,
                    isSyncedFromPlaylist: true));
            }

            return new InnerTubePlaylistParseResult(tracks, continuationToken, ownerName, ownerAvatar);
        }
        catch
        {
            return new InnerTubePlaylistParseResult([], null, inheritedOwnerName, inheritedOwnerAvatar);
        }
    }

    private static void TraverseRenderers(
        JsonElement element,
        List<InnerTubePlaylistTrackInfo> items,
        ref string? continuationToken,
        ref bool hasAnyExplicitContributor)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty prop in element.EnumerateObject())
            {
                if (prop.NameEquals("playlistVideoRenderer") && prop.Value.ValueKind == JsonValueKind.Object)
                {
                    if (TryParseWebPlaylistVideo(prop.Value, out InnerTubePlaylistTrackInfo parsed, out bool hasContrib))
                    {
                        items.Add(parsed);
                        if (hasContrib)
                        {
                            hasAnyExplicitContributor = true;
                        }
                    }

                    continue;
                }

                if (prop.NameEquals("musicResponsiveListItemRenderer") && prop.Value.ValueKind == JsonValueKind.Object)
                {
                    if (TryParseMusicPlaylistItem(prop.Value, out InnerTubePlaylistTrackInfo parsed, out bool hasContrib))
                    {
                        items.Add(parsed);
                        if (hasContrib)
                        {
                            hasAnyExplicitContributor = true;
                        }
                    }

                    continue;
                }

                if (continuationToken is null && prop.NameEquals("continuationCommand") && prop.Value.ValueKind == JsonValueKind.Object)
                {
                    if (prop.Value.TryGetProperty("token", out JsonElement tokEl) && tokEl.ValueKind == JsonValueKind.String)
                    {
                        continuationToken = tokEl.GetString();
                    }
                }

                TraverseRenderers(prop.Value, items, ref continuationToken, ref hasAnyExplicitContributor);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement child in element.EnumerateArray())
            {
                TraverseRenderers(child, items, ref continuationToken, ref hasAnyExplicitContributor);
            }
        }
    }

    private static bool TryParseWebPlaylistVideo(JsonElement renderer, out InnerTubePlaylistTrackInfo parsed, out bool hasExplicitContributor)
    {
        parsed = default;
        hasExplicitContributor = false;

        string? videoId = renderer.TryGetProperty("videoId", out JsonElement idEl) ? idEl.GetString() : null;
        if (string.IsNullOrWhiteSpace(videoId))
        {
            return false;
        }

        string title = ExtractText(renderer, "title") ?? videoId;
        string? artist = ExtractText(renderer, "shortBylineText") ?? ExtractText(renderer, "longBylineText");
        string? contributorName = null;
        Uri? contributorAvatar = null;

        if (renderer.TryGetProperty("contributors", out JsonElement contribs) &&
            contribs.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement contrib in contribs.EnumerateArray())
            {
                contributorName ??= CleanContributorName(ExtractText(contrib, "contributorName") ?? ExtractText(contrib, "name"));
                contributorAvatar ??= ExtractAvatarUri(contrib, "contributorAvatar") ?? ExtractAvatarUri(contrib, "avatar");
                if (contributorName is not null || contributorAvatar is not null)
                {
                    hasExplicitContributor = true;
                    break;
                }
            }
        }

        if (contributorName is null && renderer.TryGetProperty("contributorName", out _))
        {
            contributorName = CleanContributorName(ExtractText(renderer, "contributorName"));
            contributorAvatar ??= ExtractAvatarUri(renderer, "contributorAvatar");
            hasExplicitContributor = contributorName is not null || contributorAvatar is not null;
        }

        if (contributorName is null)
        {
            string? bylineCandidate = ExtractAddedByFromRuns(renderer, "bylineText") ?? ExtractAddedByFromRuns(renderer, "videoInfo");
            if (!string.IsNullOrWhiteSpace(bylineCandidate))
            {
                contributorName = bylineCandidate;
                contributorAvatar ??= ExtractAvatarUri(renderer, "contributorAvatar");
                hasExplicitContributor = true;
            }
        }

        parsed = new InnerTubePlaylistTrackInfo(videoId.Trim(), title, artist, contributorName, contributorAvatar);
        return true;
    }

    private static bool TryParseMusicPlaylistItem(JsonElement renderer, out InnerTubePlaylistTrackInfo parsed, out bool hasExplicitContributor)
    {
        parsed = default;
        hasExplicitContributor = false;

        string? videoId = null;
        if (renderer.TryGetProperty("playNavigationEndpoint", out JsonElement nav) &&
            nav.TryGetProperty("watchEndpoint", out JsonElement watch) &&
            watch.TryGetProperty("videoId", out JsonElement vidEl))
        {
            videoId = vidEl.GetString();
        }
        else if (renderer.TryGetProperty("playlistItemData", out JsonElement pid) &&
                 pid.TryGetProperty("videoId", out JsonElement pidVid))
        {
            videoId = pidVid.GetString();
        }

        if (string.IsNullOrWhiteSpace(videoId))
        {
            return false;
        }

        string title = videoId;
        string? artist = null;

        if (renderer.TryGetProperty("flexColumns", out JsonElement cols) && cols.ValueKind == JsonValueKind.Array)
        {
            int colIdx = 0;
            foreach (JsonElement col in cols.EnumerateArray())
            {
                if (col.TryGetProperty("musicResponsiveListItemFlexColumnRenderer", out JsonElement flex))
                {
                    string? colText = ExtractText(flex, "text");
                    if (colIdx == 0 && !string.IsNullOrWhiteSpace(colText))
                    {
                        title = colText;
                    }
                    else if (colIdx == 1 && !string.IsNullOrWhiteSpace(colText))
                    {
                        artist = colText;
                    }
                }

                colIdx++;
            }
        }

        string? contributorName = null;
        Uri? contributorAvatar = null;

        if (renderer.TryGetProperty("itemColumnData", out JsonElement colData) &&
            colData.TryGetProperty("musicCollaborativePlaylistContributedByItemData", out JsonElement contribData))
        {
            contributorName = CleanContributorName(ExtractText(contribData, "contributorName"));
            contributorAvatar = ExtractAvatarUri(contribData, "contributorAvatar");
            hasExplicitContributor = contributorName is not null || contributorAvatar is not null;
        }

        parsed = new InnerTubePlaylistTrackInfo(videoId.Trim(), title, artist, contributorName, contributorAvatar);
        return true;
    }

    private static (string? OwnerName, Uri? OwnerAvatar) ExtractPlaylistOwner(JsonElement root)
    {
        if (!root.TryGetProperty("header", out JsonElement header) || header.ValueKind != JsonValueKind.Object)
        {
            return (null, null);
        }

        if (header.TryGetProperty("playlistHeaderRenderer", out JsonElement phr))
        {
            string? name = ExtractText(phr, "ownerText");
            Uri? avatar = ExtractAvatarUri(phr, "ownerAvatar") ?? ExtractAvatarUri(phr, "avatar");
            return (name, avatar);
        }

        if (header.TryGetProperty("musicEditablePlaylistDetailHeaderRenderer", out JsonElement editHdr) &&
            editHdr.TryGetProperty("header", out JsonElement innerHdr) &&
            innerHdr.TryGetProperty("musicDetailHeaderRenderer", out JsonElement mdhr))
        {
            return (ExtractText(mdhr, "straplineTextOne"), ExtractAvatarUri(mdhr, "straplineThumbnail"));
        }

        if (header.TryGetProperty("musicDetailHeaderRenderer", out JsonElement directMdhr))
        {
            return (ExtractText(directMdhr, "straplineTextOne"), ExtractAvatarUri(directMdhr, "straplineThumbnail"));
        }

        return (null, null);
    }

    private static string? ExtractText(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out JsonElement el))
        {
            return null;
        }

        if (el.ValueKind == JsonValueKind.String)
        {
            return el.GetString();
        }

        if (el.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (el.TryGetProperty("simpleText", out JsonElement simple) && simple.ValueKind == JsonValueKind.String)
        {
            return simple.GetString();
        }

        if (el.TryGetProperty("runs", out JsonElement runs) && runs.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement run in runs.EnumerateArray())
            {
                if (run.TryGetProperty("text", out JsonElement txt) &&
                    txt.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrWhiteSpace(txt.GetString()))
                {
                    return txt.GetString();
                }
            }
        }

        return null;
    }

    private static string? ExtractAddedByFromRuns(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out JsonElement el) ||
            el.ValueKind != JsonValueKind.Object ||
            !el.TryGetProperty("runs", out JsonElement runs) ||
            runs.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (JsonElement run in runs.EnumerateArray())
        {
            string? text = run.TryGetProperty("text", out JsonElement txt) ? txt.GetString() : null;
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            foreach (string prefix in _addedByPrefixes)
            {
                if (text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    string cleaned = text[prefix.Length..].Trim();
                    if (!string.IsNullOrEmpty(cleaned))
                    {
                        return cleaned;
                    }
                }
            }
        }

        return null;
    }

    private static string? CleanContributorName(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        string trimmed = raw.Trim();
        foreach (string prefix in _addedByPrefixes)
        {
            if (trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed[prefix.Length..].Trim();
                break;
            }
        }

        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static Uri? ExtractAvatarUri(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out JsonElement avatarNode) || avatarNode.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        JsonElement thumbContainer = avatarNode;
        if (avatarNode.TryGetProperty("musicThumbnailRenderer", out JsonElement mtr) &&
            mtr.TryGetProperty("thumbnail", out JsonElement innerThumb))
        {
            thumbContainer = innerThumb;
        }

        if (!thumbContainer.TryGetProperty("thumbnails", out JsonElement thumbs) || thumbs.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        Uri? best = null;
        int bestWidth = 0;

        foreach (JsonElement thumb in thumbs.EnumerateArray())
        {
            string? rawUrl = thumb.TryGetProperty("url", out JsonElement urlEl) ? urlEl.GetString() : null;
            if (string.IsNullOrWhiteSpace(rawUrl))
            {
                continue;
            }

            if (rawUrl.StartsWith("//", StringComparison.Ordinal))
            {
                rawUrl = "https:" + rawUrl;
            }

            if (!Uri.TryCreate(rawUrl, UriKind.Absolute, out Uri? candidate))
            {
                continue;
            }

            Uri? sanitized = Track.SanitizeAvatarUri(candidate);
            if (sanitized is null)
            {
                continue;
            }

            int width = thumb.TryGetProperty("width", out JsonElement wEl) && wEl.TryGetInt32(out int w) ? w : 64;
            if (best is null || (width >= bestWidth && width <= 176))
            {
                best = sanitized;
                bestWidth = width;
            }
        }

        return best;
    }
}
