namespace burroQueue.Infrastructure.Network.Messages;

public sealed record HostHandoffMessage(
    Guid GroupId,
    Guid PreviousHostClientId,
    string ActiveVideoId,
    int CurrentTrackIndex,
    double PlaybackPositionSeconds,
    bool WasPlaying,
    DateTimeOffset Timestamp);

