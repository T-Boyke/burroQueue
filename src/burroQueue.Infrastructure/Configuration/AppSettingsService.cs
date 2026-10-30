using System.Globalization;
using System.Text;
using System.Text.Json;
using burroQueue.Infrastructure.Persistence;

namespace burroQueue.Infrastructure.Configuration;

public sealed class AppSettingsService : IAppSettingsService
{
    private readonly string _configFilePath;
    private AppSettingsData _currentSettings = new();

    public AppSettingsData CurrentSettings => _currentSettings;

    private bool IsJsonMode => _configFilePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase);

    public AppSettingsService(string? customPath = null)
    {
        _configFilePath = customPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "burroQueue",
            "settings.toml");
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (IsJsonMode)
        {
            _currentSettings = await TryLoadLegacyJsonAsync(_configFilePath, cancellationToken).ConfigureAwait(false) ?? new AppSettingsData();
            return;
        }

        if (!File.Exists(_configFilePath))
        {
            string? dir = Path.GetDirectoryName(_configFilePath);
            string legacyJson = !string.IsNullOrEmpty(dir)
                ? Path.Combine(dir, "settings.json")
                : string.Empty;

            if (!string.IsNullOrEmpty(legacyJson) && File.Exists(legacyJson))
            {
                AppSettingsData? migrated = await TryLoadLegacyJsonAsync(legacyJson, cancellationToken).ConfigureAwait(false);
                if (migrated is not null)
                {
                    _currentSettings = migrated;
                    await SaveAsync(migrated, cancellationToken).ConfigureAwait(false);
                    return;
                }
            }

            _currentSettings = new AppSettingsData();
            return;
        }

        try
        {
            string rawToml = await File.ReadAllTextAsync(_configFilePath, cancellationToken).ConfigureAwait(false);
            var (sections, _) = TomlDocumentSerializer.Parse(rawToml);

            sections.TryGetValue("general", out Dictionary<string, string>? general);
            sections.TryGetValue("group", out Dictionary<string, string>? group);
            sections.TryGetValue("voip", out Dictionary<string, string>? voip);

            string playlistUrl = TomlDocumentSerializer.ParseString(group, "youtube_playlist_url");

            _currentSettings = new AppSettingsData
            {
                AppLanguage = TomlDocumentSerializer.ParseString(general, "language", "Auto"),
                WindowBackdrop = TomlDocumentSerializer.ParseString(general, "backdrop", "Mica"),
                UseSystemAccentColor = TomlDocumentSerializer.ParseBool(general, "use_system_accent", true),
                EnableWebDropServer = TomlDocumentSerializer.ParseBool(general, "web_drop_enabled", false),
                ClipboardDetectionMode = TomlDocumentSerializer.ParseString(general, "clipboard_mode", "AskToast"),
                MaxMusicDurationMinutes = TomlDocumentSerializer.ParseInt(general, "max_music_duration_minutes", 12),
                ClientDisplayName = TomlDocumentSerializer.ParseString(group, "client_display_name", Environment.UserName),
                CollaborativePlaylistUrl = string.IsNullOrWhiteSpace(playlistUrl) ? null : playlistUrl,
                AutoOpenCollaborativePlaylistOnJoin = TomlDocumentSerializer.ParseBool(group, "auto_open_playlist_on_join", true),
                HasJoinedCollaborativePlaylist = TomlDocumentSerializer.ParseBool(group, "has_joined_playlist", false),
                AutoSyncCollaborativePlaylist = TomlDocumentSerializer.ParseBool(group, "auto_sync_playlist", true),
                AutoSyncPlaylistIntervalSeconds = TomlDocumentSerializer.ParseInt(group, "auto_sync_interval_seconds", 60),
                SelectedTelephonyProviderId = TomlDocumentSerializer.ParseString(voip, "provider_id", "SwyxIt"),
                SelectedAudioDeviceId = TomlDocumentSerializer.ParseString(voip, "audio_device_id", string.Empty),
                DuckingReleaseDelaySeconds = TomlDocumentSerializer.ParseInt(voip, "ducking_release_delay_seconds", 3),
                MasterVolume = TomlDocumentSerializer.ParseDouble(voip, "master_volume", 0.8),
                EnableLoudnessNormalization = TomlDocumentSerializer.ParseBool(voip, "loudness_normalization_enabled", true),
                TargetLoudnessLufs = Math.Clamp(TomlDocumentSerializer.ParseDouble(voip, "target_loudness_lufs", -14.0), -24.0, -6.0)
            };
        }
        catch
        {
            _currentSettings = new AppSettingsData();
        }
    }

    public async Task SaveAsync(AppSettingsData settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _currentSettings = settings;

        if (IsJsonMode)
        {
            string? directory = Path.GetDirectoryName(_configFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await using FileStream stream = File.Create(_configFilePath);
            await JsonSerializer.SerializeAsync(stream, settings, cancellationToken: cancellationToken).ConfigureAwait(false);
            return;
        }

        StringBuilder sb = new(512);
        sb.AppendLine("# burroQueue Settings (TOML)");
        sb.AppendLine("[general]");
        sb.AppendLine(CultureInfo.InvariantCulture, $"language = {TomlDocumentSerializer.EscapeString(settings.AppLanguage)}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"backdrop = {TomlDocumentSerializer.EscapeString(settings.WindowBackdrop)}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"use_system_accent = {(settings.UseSystemAccentColor ? "true" : "false")}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"web_drop_enabled = {(settings.EnableWebDropServer ? "true" : "false")}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"clipboard_mode = {TomlDocumentSerializer.EscapeString(settings.ClipboardDetectionMode)}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"max_music_duration_minutes = {settings.MaxMusicDurationMinutes}");
        sb.AppendLine();

        sb.AppendLine("[group]");
        sb.AppendLine(CultureInfo.InvariantCulture, $"client_display_name = {TomlDocumentSerializer.EscapeString(settings.ClientDisplayName)}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"youtube_playlist_url = {TomlDocumentSerializer.EscapeString(settings.CollaborativePlaylistUrl ?? string.Empty)}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"auto_open_playlist_on_join = {(settings.AutoOpenCollaborativePlaylistOnJoin ? "true" : "false")}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"has_joined_playlist = {(settings.HasJoinedCollaborativePlaylist ? "true" : "false")}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"auto_sync_playlist = {(settings.AutoSyncCollaborativePlaylist ? "true" : "false")}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"auto_sync_interval_seconds = {settings.AutoSyncPlaylistIntervalSeconds}");
        sb.AppendLine();

        sb.AppendLine("[voip]");
        sb.AppendLine(CultureInfo.InvariantCulture, $"provider_id = {TomlDocumentSerializer.EscapeString(settings.SelectedTelephonyProviderId)}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"audio_device_id = {TomlDocumentSerializer.EscapeString(settings.SelectedAudioDeviceId)}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"ducking_release_delay_seconds = {settings.DuckingReleaseDelaySeconds}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"master_volume = {settings.MasterVolume}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"loudness_normalization_enabled = {(settings.EnableLoudnessNormalization ? "true" : "false")}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"target_loudness_lufs = {Math.Clamp(settings.TargetLoudnessLufs, -24.0, -6.0)}");

        await TomlDocumentSerializer.WriteAtomicallyAsync(_configFilePath, sb.ToString(), cancellationToken).ConfigureAwait(false);
    }

    private static async Task<AppSettingsData?> TryLoadLegacyJsonAsync(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            await using FileStream stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<AppSettingsData>(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }
}
