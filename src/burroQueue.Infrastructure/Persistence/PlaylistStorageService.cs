using System.IO;
using System.Text.Json;
using burroQueue.Domain.Model;
using burroQueue.Infrastructure.Configuration;

namespace burroQueue.Infrastructure.Persistence;

public sealed class PlaylistStorageService : IPlaylistStorageService
{
    private readonly string _storageFilePath;

    public PlaylistStorageService(string? customPath = null)
    {
        _storageFilePath = customPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "burroQueue",
            "playlist_state.json");
    }

    public async Task SavePlaylistAsync(Playlist playlist, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(playlist);

        string? dir = Path.GetDirectoryName(_storageFilePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        List<StoredTrackDto> tracks = [];
        if (playlist.CurrentTrack is not null)
        {
            tracks.Add(StoredTrackDto.FromDomain(playlist.CurrentTrack, isCurrent: true));
        }

        foreach (Track item in playlist.QueuedTracks)
        {
            tracks.Add(StoredTrackDto.FromDomain(item, isCurrent: false));
        }

        await using FileStream stream = File.Create(_storageFilePath);
        await JsonSerializer.SerializeAsync(stream, tracks, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task LoadPlaylistAsync(Playlist playlist, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(playlist);

        if (!File.Exists(_storageFilePath))
        {
            return;
        }

        try
        {
            await using FileStream stream = File.OpenRead(_storageFilePath);
            List<StoredTrackDto>? tracks = await JsonSerializer.DeserializeAsync<List<StoredTrackDto>>(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (tracks is null || tracks.Count == 0)
            {
                return;
            }

            playlist.Clear();
            foreach (StoredTrackDto dto in tracks)
            {
                Track track = dto.ToDomain();
                playlist.Enqueue(track);
            }
        }
        catch
        {
            // Ignoriere korrupte State-Dateien und starte sauber
        }
    }
}
