using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using burroQueue.Domain.Model;
using burroQueue.Infrastructure.Configuration;

namespace burroQueue.Infrastructure.Persistence;

public sealed class PlaylistStorageService : IPlaylistStorageService
{
    private readonly string _storageFilePath;
    private QueuePlaybackCursor _pendingCursor = QueuePlaybackCursor.Empty;

    public QueuePlaybackCursor LastLoadedCursor { get; private set; } = QueuePlaybackCursor.Empty;

    private bool IsJsonMode => _storageFilePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase);

    public PlaylistStorageService(string? customPath = null)
    {
        _storageFilePath = customPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "burroQueue",
            "session.toml");
    }

    public void UpdatePlaybackCursor(QueuePlaybackCursor cursor)
    {
        _pendingCursor = cursor;
    }

    public Task SaveSessionAsync(Playlist playlist, QueuePlaybackCursor cursor, CancellationToken cancellationToken = default)
    {
        _pendingCursor = cursor;
        return SavePlaylistAsync(playlist, cancellationToken);
    }

    public async Task<QueuePlaybackCursor> LoadSessionAsync(Playlist playlist, CancellationToken cancellationToken = default)
    {
        await LoadPlaylistAsync(playlist, cancellationToken).ConfigureAwait(false);
        return LastLoadedCursor;
    }

    public async Task SavePlaylistAsync(Playlist playlist, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(playlist);

        if (IsJsonMode)
        {
            string? dir = Path.GetDirectoryName(_storageFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            List<StoredTrackDto> tracks = new(playlist.AllTracks.Count);
            for (int i = 0; i < playlist.AllTracks.Count; i++)
            {
                tracks.Add(StoredTrackDto.FromDomain(playlist.AllTracks[i], isCurrent: i == playlist.CurrentTrackIndex));
            }

            await using FileStream stream = new(_storageFilePath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
            await JsonSerializer.SerializeAsync(stream, tracks, cancellationToken: cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        string activeVideoId = !string.IsNullOrWhiteSpace(_pendingCursor.ActiveVideoId)
            ? _pendingCursor.ActiveVideoId
            : (playlist.CurrentTrack?.VideoId ?? string.Empty);

        int activeIdx = _pendingCursor.QueueIndex >= 0
            ? _pendingCursor.QueueIndex
            : playlist.CurrentTrackIndex;

        double posSec = Math.Max(0.0, _pendingCursor.PlaybackPosition.TotalSeconds);

        StringBuilder sb = new(1024);
        sb.AppendLine("# burroQueue Session & Playback Cursor (TOML)");
        sb.AppendLine("[session]");
        sb.AppendLine(CultureInfo.InvariantCulture, $"active_video_id = {TomlDocumentSerializer.EscapeString(activeVideoId)}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"active_queue_index = {activeIdx}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"playback_position_seconds = {posSec:0.###}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"was_playing = {(_pendingCursor.WasPlaying ? "true" : "false")}");

        foreach (Track item in playlist.AllTracks)
        {
            sb.AppendLine();
            sb.AppendLine("[[session.queue]]");
            sb.AppendLine(CultureInfo.InvariantCulture, $"video_id = {TomlDocumentSerializer.EscapeString(item.VideoId)}");
            sb.AppendLine(CultureInfo.InvariantCulture, $"url = {TomlDocumentSerializer.EscapeString(item.Url.ToString())}");
            sb.AppendLine(CultureInfo.InvariantCulture, $"title = {TomlDocumentSerializer.EscapeString(item.Title)}");
            sb.AppendLine(CultureInfo.InvariantCulture, $"added_by = {TomlDocumentSerializer.EscapeString(item.SubmittedBy)}");
            sb.AppendLine(CultureInfo.InvariantCulture, $"artist_name = {TomlDocumentSerializer.EscapeString(item.ArtistName ?? string.Empty)}");
            sb.AppendLine(CultureInfo.InvariantCulture, $"submitter_avatar_url = {TomlDocumentSerializer.EscapeString(item.SubmitterAvatarUrl?.ToString() ?? string.Empty)}");
            sb.AppendLine(CultureInfo.InvariantCulture, $"is_synced = {(item.IsSyncedFromPlaylist ? "true" : "false")}");
            sb.AppendLine(CultureInfo.InvariantCulture, $"enqueued_at = {TomlDocumentSerializer.EscapeString(item.EnqueuedAtUtc.ToString("O", CultureInfo.InvariantCulture))}");
        }

        await TomlDocumentSerializer.WriteAtomicallyAsync(_storageFilePath, sb.ToString(), cancellationToken).ConfigureAwait(false);
    }

    public async Task LoadPlaylistAsync(Playlist playlist, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(playlist);
        LastLoadedCursor = QueuePlaybackCursor.Empty;

        if (IsJsonMode)
        {
            await TryLoadLegacyJsonAsync(_storageFilePath, playlist, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (!File.Exists(_storageFilePath))
        {
            string? dir = Path.GetDirectoryName(_storageFilePath);
            string legacyJson = !string.IsNullOrEmpty(dir)
                ? Path.Combine(dir, "playlist_state.json")
                : string.Empty;

            if (!string.IsNullOrEmpty(legacyJson) && File.Exists(legacyJson))
            {
                bool migrated = await TryLoadLegacyJsonAsync(legacyJson, playlist, cancellationToken).ConfigureAwait(false);
                if (migrated)
                {
                    await SavePlaylistAsync(playlist, cancellationToken).ConfigureAwait(false);
                }
            }

            return;
        }

        try
        {
            string rawToml = await File.ReadAllTextAsync(_storageFilePath, cancellationToken).ConfigureAwait(false);
            var (sections, tableArrays) = TomlDocumentSerializer.Parse(rawToml);

            sections.TryGetValue("session", out Dictionary<string, string>? sessionSec);
            string activeVid = TomlDocumentSerializer.ParseString(sessionSec, "active_video_id");
            int activeIdx = TomlDocumentSerializer.ParseInt(sessionSec, "active_queue_index", -1);
            double posSeconds = TomlDocumentSerializer.ParseDouble(sessionSec, "playback_position_seconds", 0.0);
            bool wasPlaying = TomlDocumentSerializer.ParseBool(sessionSec, "was_playing", false);

            if (!tableArrays.TryGetValue("session.queue", out List<Dictionary<string, string>>? entries) || entries.Count == 0)
            {
                return;
            }

            playlist.Clear();
            foreach (Dictionary<string, string> entry in entries)
            {
                string vid = TomlDocumentSerializer.ParseString(entry, "video_id");
                string urlStr = TomlDocumentSerializer.ParseString(entry, "url");
                if (string.IsNullOrWhiteSpace(vid) || !Uri.TryCreate(urlStr, UriKind.Absolute, out Uri? url))
                {
                    continue;
                }

                string title = TomlDocumentSerializer.ParseString(entry, "title", vid);
                string addedBy = TomlDocumentSerializer.ParseString(entry, "added_by", "Anonymous Burro");
                string artist = TomlDocumentSerializer.ParseString(entry, "artist_name");
                string avatarStr = TomlDocumentSerializer.ParseString(entry, "submitter_avatar_url");
                bool isSynced = TomlDocumentSerializer.ParseBool(entry, "is_synced", false);
                string enqueuedStr = TomlDocumentSerializer.ParseString(entry, "enqueued_at");

                DateTimeOffset enqueuedAt = DateTimeOffset.TryParse(enqueuedStr, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTimeOffset parsedTs)
                    ? parsedTs
                    : DateTimeOffset.UtcNow;

                Uri? avatarUri = !string.IsNullOrWhiteSpace(avatarStr) && Uri.TryCreate(avatarStr, UriKind.Absolute, out Uri? parsedAvatar)
                    ? parsedAvatar
                    : null;

                playlist.Enqueue(new Track(
                    vid,
                    url,
                    title,
                    addedBy,
                    enqueuedAt,
                    artistName: string.IsNullOrWhiteSpace(artist) ? null : artist,
                    submitterAvatarUrl: avatarUri,
                    isSyncedFromPlaylist: isSynced));
            }

            if (activeIdx >= 0 && activeIdx < playlist.AllTracks.Count)
            {
                playlist.JumpToIndex(activeIdx);
            }
            else if (!string.IsNullOrWhiteSpace(activeVid))
            {
                for (int i = 0; i < playlist.AllTracks.Count; i++)
                {
                    if (string.Equals(playlist.AllTracks[i].VideoId, activeVid, StringComparison.Ordinal))
                    {
                        playlist.JumpToIndex(i);
                        activeIdx = i;
                        break;
                    }
                }
            }

            LastLoadedCursor = new QueuePlaybackCursor(
                activeVid,
                playlist.CurrentTrackIndex,
                TimeSpan.FromSeconds(Math.Max(0.0, posSeconds)),
                wasPlaying);
            _pendingCursor = LastLoadedCursor;
        }
        catch
        {
            // Ignoriere korrupte State-Dateien
        }
    }

    private async Task<bool> TryLoadLegacyJsonAsync(string path, Playlist playlist, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            await using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            List<StoredTrackDto>? tracks = await JsonSerializer.DeserializeAsync<List<StoredTrackDto>>(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (tracks is null || tracks.Count == 0)
            {
                return false;
            }

            playlist.Clear();
            int currentIdx = -1;
            for (int i = 0; i < tracks.Count; i++)
            {
                if (tracks[i].IsCurrent && currentIdx < 0)
                {
                    currentIdx = i;
                }

                playlist.Enqueue(tracks[i].ToDomain());
            }

            if (currentIdx >= 0)
            {
                playlist.JumpToIndex(currentIdx);
            }

            LastLoadedCursor = new QueuePlaybackCursor(
                playlist.CurrentTrack?.VideoId ?? string.Empty,
                playlist.CurrentTrackIndex,
                TimeSpan.Zero,
                WasPlaying: false);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
