using burroQueue.Domain.Model;
using CommunityToolkit.Mvvm.ComponentModel;

namespace burroQueue.Presentation.ViewModels;

public sealed partial class QueueTrackItemViewModel : ObservableObject
{
    public Track Track { get; }

    public TrackId Id => Track.Id;

    public string VideoId => Track.VideoId;

    public string Title => Track.Title;

    public string SubmittedBy => Track.SubmittedBy;

    public string? ArtistName => Track.ArtistName;

    public Uri? SubmitterAvatarUrl => Track.SubmitterAvatarUrl;

    public string ThumbnailUrl => Track.ThumbnailUrl;

    [ObservableProperty]
    private bool _isCurrentlyPlaying;

    [ObservableProperty]
    private bool _hasPlayed;

    [ObservableProperty]
    private int _upvoteCount;

    [ObservableProperty]
    private bool _isUpvotedByLocalUser;

    public QueueTrackItemViewModel(
        Track track,
        bool isCurrentlyPlaying = false,
        bool hasPlayed = false,
        int upvoteCount = 0,
        bool isUpvotedByLocalUser = false)
    {
        Track = track ?? throw new ArgumentNullException(nameof(track));
        _isCurrentlyPlaying = isCurrentlyPlaying;
        _hasPlayed = hasPlayed;
        _upvoteCount = upvoteCount;
        _isUpvotedByLocalUser = isUpvotedByLocalUser;
    }

    public void RefreshMetadata()
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(SubmittedBy));
        OnPropertyChanged(nameof(ArtistName));
        OnPropertyChanged(nameof(SubmitterAvatarUrl));
    }
}

