using burroQueue.Domain.Model;

namespace burroQueue.Presentation.Services;

public interface ISystemMediaControlsService : IDisposable
{
    event EventHandler? PlayPauseRequested;

    event EventHandler? NextTrackRequested;

    string CurrentTitle { get; }

    string CurrentArtist { get; }

    string? CurrentThumbnailUrl { get; }

    PlaybackState CurrentPlaybackState { get; }

    void InitializeForWindow(IntPtr windowHandle);

    void UpdatePlaybackState(PlaybackState state);

    void UpdateTrackMetadata(string title, string artist, string? thumbnailUrl = null);
}
