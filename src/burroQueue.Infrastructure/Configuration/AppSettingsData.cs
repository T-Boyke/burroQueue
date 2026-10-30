namespace burroQueue.Infrastructure.Configuration;

public sealed class AppSettingsData
{
    public string SelectedTelephonyProviderId { get; set; } = "SwyxIt";

    public string SelectedAudioDeviceId { get; set; } = string.Empty;

    public string? CollaborativePlaylistUrl { get; set; }

    public bool AutoOpenCollaborativePlaylistOnJoin { get; set; } = true;

    public string ClientDisplayName { get; set; } = Environment.UserName;

    public double MasterVolume { get; set; } = 0.8;

    public bool EnableWebDropServer { get; set; }

    public bool HasJoinedCollaborativePlaylist { get; set; }

    public int DuckingReleaseDelaySeconds { get; set; } = 3;

    public bool AutoSyncCollaborativePlaylist { get; set; } = true;

    public int AutoSyncPlaylistIntervalSeconds { get; set; } = 60;

    public string WindowBackdrop { get; set; } = "Mica";

    public bool UseSystemAccentColor { get; set; } = true;

    public string AppLanguage { get; set; } = "Auto";

    public string ClipboardDetectionMode { get; set; } = "AskToast";

    public int MaxMusicDurationMinutes { get; set; } = 12;

    public bool EnableLoudnessNormalization { get; set; } = true;

    public double TargetLoudnessLufs { get; set; } = -14.0;
}
