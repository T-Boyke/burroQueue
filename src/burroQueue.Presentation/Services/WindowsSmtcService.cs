using burroQueue.Domain.Model;
using Windows.Media;
using Windows.Storage.Streams;

namespace burroQueue.Presentation.Services;

public sealed class WindowsSmtcService : ISystemMediaControlsService
{
    private SystemMediaTransportControls? _smtc;

    public event EventHandler? PlayPauseRequested;

    public event EventHandler? NextTrackRequested;

    public string CurrentTitle { get; private set; } = string.Empty;

    public string CurrentArtist { get; private set; } = string.Empty;

    public string? CurrentThumbnailUrl { get; private set; }

    public PlaybackState CurrentPlaybackState { get; private set; } = PlaybackState.Stopped;

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    public void InitializeForWindow(IntPtr windowHandle)
    {
        if (windowHandle == IntPtr.Zero || _smtc is not null)
        {
            return;
        }

        try
        {
            _smtc = SystemMediaTransportControlsInterop.GetForWindow(windowHandle);
            _smtc.IsEnabled = true;
            _smtc.IsPlayEnabled = true;
            _smtc.IsPauseEnabled = true;
            _smtc.IsNextEnabled = true;
            _smtc.IsStopEnabled = true;
            _smtc.ButtonPressed += OnSmtcButtonPressed;
            ApplyPlaybackStatusToNative(CurrentPlaybackState);
            ApplyMetadataToNative(CurrentTitle, CurrentArtist, CurrentThumbnailUrl);
        }
        catch
        {
            // Headless or non-WinRT shell fallback
        }
    }

    public void UpdatePlaybackState(PlaybackState state)
    {
        CurrentPlaybackState = state;
        ApplyPlaybackStatusToNative(state);
    }

    public void UpdateTrackMetadata(string title, string artist, string? thumbnailUrl = null)
    {
        CurrentTitle = title ?? string.Empty;
        CurrentArtist = artist ?? string.Empty;
        CurrentThumbnailUrl = thumbnailUrl;
        ApplyMetadataToNative(CurrentTitle, CurrentArtist, CurrentThumbnailUrl);
    }

    internal void SimulateButtonPressed(string buttonName)
    {
        if (string.Equals(buttonName, "Play", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(buttonName, "Pause", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(buttonName, "PlayPause", StringComparison.OrdinalIgnoreCase))
        {
            PlayPauseRequested?.Invoke(this, EventArgs.Empty);
        }
        else if (string.Equals(buttonName, "Next", StringComparison.OrdinalIgnoreCase))
        {
            NextTrackRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private void OnSmtcButtonPressed(SystemMediaTransportControls sender, SystemMediaTransportControlsButtonPressedEventArgs args)
    {
        switch (args.Button)
        {
            case SystemMediaTransportControlsButton.Play:
            case SystemMediaTransportControlsButton.Pause:
            case SystemMediaTransportControlsButton.Stop:
                PlayPauseRequested?.Invoke(this, EventArgs.Empty);
                break;

            case SystemMediaTransportControlsButton.Next:
                NextTrackRequested?.Invoke(this, EventArgs.Empty);
                break;
        }
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private void ApplyPlaybackStatusToNative(PlaybackState state)
    {
        if (_smtc is null)
        {
            return;
        }

        try
        {
            _smtc.PlaybackStatus = state switch
            {
                PlaybackState.Playing => MediaPlaybackStatus.Playing,
                PlaybackState.Paused => MediaPlaybackStatus.Paused,
                _ => MediaPlaybackStatus.Stopped
            };
        }
        catch
        {
        }
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private void ApplyMetadataToNative(string title, string artist, string? thumbnailUrl)
    {
        if (_smtc is null || string.IsNullOrWhiteSpace(title))
        {
            return;
        }

        try
        {
            SystemMediaTransportControlsDisplayUpdater updater = _smtc.DisplayUpdater;
            updater.Type = MediaPlaybackType.Music;
            updater.MusicProperties.Title = title;
            updater.MusicProperties.Artist = artist;
            if (!string.IsNullOrWhiteSpace(thumbnailUrl) && Uri.TryCreate(thumbnailUrl, UriKind.Absolute, out Uri? thumbUri))
            {
                updater.Thumbnail = RandomAccessStreamReference.CreateFromUri(thumbUri);
            }

            updater.Update();
        }
        catch
        {
        }
    }

    public void Dispose()
    {
        if (_smtc is not null)
        {
            try
            {
                _smtc.ButtonPressed -= OnSmtcButtonPressed;
            }
            catch
            {
            }

            _smtc = null;
        }
    }
}

