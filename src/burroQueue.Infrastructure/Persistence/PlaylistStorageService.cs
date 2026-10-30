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

        List<StoredTrackDto> tracks = new(playlist.AllTracks.Count);
        for (int i = 0; i < playlist.AllTracks.Count; i++)
        {
            tracks.Add(StoredTrackDto.FromDomain(playlist.AllTracks[i], isCurrent: i == playlist.CurrentTrackIndex));
        }

        await using FileStream stream = new(_storageFilePath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
        await JsonSerializer.SerializeAsync(stream, tracks, cancellationToken: cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
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
            await using FileStream stream = new(_storageFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            List<StoredTrackDto>? tracks = await JsonSerializer.DeserializeAsync<List<StoredTrackDto>>(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (tracks is null || tracks.Count == 0)
            {
                return;
            }

            playlist.Clear();
            int currentIdx = -1;
            for (int i = 0; i < tracks.Count; i++)
            {
                StoredTrackDto dto = tracks[i];
                if (dto.IsCurrent && currentIdx < 0)
                {
                    currentIdx = i;
                }

                playlist.Enqueue(dto.ToDomain());
            }

            if (currentIdx >= 0)
            {
                playlist.JumpToIndex(currentIdx);
            }
        }
        catch
        {
            // Ignoriere korrupte State-Dateien und starte sauber
        }
    }
}
