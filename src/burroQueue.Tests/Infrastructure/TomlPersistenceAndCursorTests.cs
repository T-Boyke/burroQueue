using burroQueue.Domain.Model;
using burroQueue.Infrastructure.Audio;
using burroQueue.Infrastructure.Configuration;
using burroQueue.Infrastructure.Persistence;
using burroQueue.Infrastructure.Playback;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace burroQueue.Tests.Infrastructure;

public sealed class TomlPersistenceAndCursorTests : IDisposable
{
    private readonly string _tempDir;

    public TomlPersistenceAndCursorTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"burro_toml_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }
        catch
        {
        }
    }

    [Fact]
    public async Task AppSettingsService_WithTomlPath_ShouldWriteReadableTomlAtomicallyAndRoundTripAllFields()
    {
        string tomlPath = Path.Combine(_tempDir, "settings.toml");
        AppSettingsService writer = new(tomlPath);
        AppSettingsData original = new()
        {
            SelectedTelephonyProviderId = "Starface",
            SelectedAudioDeviceId = "dev-usb-dac",
            CollaborativePlaylistUrl = "https://www.youtube.com/playlist?list=PL_OFFICE&jct=secret_tok",
            AutoOpenCollaborativePlaylistOnJoin = false,
            ClientDisplayName = "Tobias \"T-Boyke\"",
            MasterVolume = 0.65,
            EnableWebDropServer = true,
            HasJoinedCollaborativePlaylist = true,
            DuckingReleaseDelaySeconds = 5,
            AutoSyncCollaborativePlaylist = true,
            AutoSyncPlaylistIntervalSeconds = 45,
            WindowBackdrop = "Acrylic",
            UseSystemAccentColor = false,
            AppLanguage = "de-DE",
            ClipboardDetectionMode = "AskToast"
        };

        await writer.SaveAsync(original);

        string rawToml = await File.ReadAllTextAsync(tomlPath);
        rawToml.Should().Contain("[general]");
        rawToml.Should().Contain("[group]");
        rawToml.Should().Contain("[voip]");
        File.Exists(tomlPath + ".tmp").Should().BeFalse("atomic swap must clean up temporary file");

        AppSettingsService reader = new(tomlPath);
        await reader.LoadAsync();

        reader.CurrentSettings.SelectedTelephonyProviderId.Should().Be("Starface");
        reader.CurrentSettings.SelectedAudioDeviceId.Should().Be("dev-usb-dac");
        reader.CurrentSettings.CollaborativePlaylistUrl.Should().Be("https://www.youtube.com/playlist?list=PL_OFFICE&jct=secret_tok");
        reader.CurrentSettings.AutoOpenCollaborativePlaylistOnJoin.Should().BeFalse();
        reader.CurrentSettings.ClientDisplayName.Should().Be("Tobias \"T-Boyke\"");
        reader.CurrentSettings.MasterVolume.Should().BeApproximately(0.65, 0.0001);
        reader.CurrentSettings.EnableWebDropServer.Should().BeTrue();
        reader.CurrentSettings.DuckingReleaseDelaySeconds.Should().Be(5);
        reader.CurrentSettings.AutoSyncPlaylistIntervalSeconds.Should().Be(45);
        reader.CurrentSettings.WindowBackdrop.Should().Be("Acrylic");
        reader.CurrentSettings.UseSystemAccentColor.Should().BeFalse();
        reader.CurrentSettings.AppLanguage.Should().Be("de-DE");
        reader.CurrentSettings.ClipboardDetectionMode.Should().Be("AskToast");
    }

    [Fact]
    public async Task AppSettingsService_WhenTomlMissingAndLegacyJsonExists_ShouldAutoMigrateJsonToToml()
    {
        string legacyJsonPath = Path.Combine(_tempDir, "settings.json");
        string tomlPath = Path.Combine(_tempDir, "settings.toml");

        AppSettingsService legacyWriter = new(legacyJsonPath);
        await legacyWriter.SaveAsync(new AppSettingsData
        {
            ClientDisplayName = "LegacyUser",
            MasterVolume = 0.42,
            WindowBackdrop = "Tabbed"
        });

        AppSettingsService tomlReader = new(tomlPath);
        await tomlReader.LoadAsync();

        tomlReader.CurrentSettings.ClientDisplayName.Should().Be("LegacyUser");
        tomlReader.CurrentSettings.MasterVolume.Should().BeApproximately(0.42, 0.001);
        tomlReader.CurrentSettings.WindowBackdrop.Should().Be("Tabbed");
        File.Exists(tomlPath).Should().BeTrue("one-time migration should write settings.toml");
    }

    [Fact]
    public async Task PlaylistStorageService_WithSessionToml_ShouldRoundTripTracksCollaboratorAvatarAndPlaybackCursor()
    {
        string sessionTomlPath = Path.Combine(_tempDir, "session.toml");
        PlaylistStorageService storage = new(sessionTomlPath);

        Playlist original = new();
        Uri avatar = new("https://yt3.ggpht.com/ytc/tobias_avatar=s88-c-k");
        Track t1 = new("vid11111111", new Uri("https://www.youtube.com/watch?v=vid11111111"), "History Track", "Alice", DateTimeOffset.UtcNow, artistName: "Air", submitterAvatarUrl: avatar, isSyncedFromPlaylist: true);
        Track t2 = new("vid22222222", new Uri("https://www.youtube.com/watch?v=vid22222222"), "Active Track", "Tobias", DateTimeOffset.UtcNow, artistName: "Justice", submitterAvatarUrl: avatar, isSyncedFromPlaylist: true);
        Track t3 = new("vid33333333", new Uri("https://www.youtube.com/watch?v=vid33333333"), "Upcoming Track", "Bob", DateTimeOffset.UtcNow);
        original.Enqueue(t1);
        original.Enqueue(t2);
        original.Enqueue(t3);
        original.JumpToIndex(1);

        QueuePlaybackCursor cursor = new("vid22222222", QueueIndex: 1, TimeSpan.FromSeconds(83.5), WasPlaying: true);
        await storage.SaveSessionAsync(original, cursor);

        string rawToml = await File.ReadAllTextAsync(sessionTomlPath);
        rawToml.Should().Contain("[session]");
        rawToml.Should().Contain("[[session.queue]]");
        rawToml.Should().Contain("playback_position_seconds = 83.5");

        Playlist restored = new();
        QueuePlaybackCursor loadedCursor = await storage.LoadSessionAsync(restored);

        loadedCursor.ActiveVideoId.Should().Be("vid22222222");
        loadedCursor.QueueIndex.Should().Be(1);
        loadedCursor.PlaybackPosition.TotalSeconds.Should().BeApproximately(83.5, 0.01);
        loadedCursor.WasPlaying.Should().BeTrue();

        restored.AllTracks.Should().HaveCount(3);
        restored.CurrentTrackIndex.Should().Be(1);
        restored.CurrentTrack!.Title.Should().Be("Active Track");
        restored.CurrentTrack.ArtistName.Should().Be("Justice");
        restored.CurrentTrack.SubmitterAvatarUrl.Should().Be(avatar);
    }

    [Fact]
    public async Task AudioPlayerBackend_PreparePausedAtPositionAsync_ShouldOpenPauseAndSeekStream()
    {
        IAudioOutputController audioOut = Substitute.For<IAudioOutputController>();
        audioOut.CurrentVolume.Returns(0.75);
        IStreamUriResolver resolver = Substitute.For<IStreamUriResolver>();
        Uri streamUri = new("https://cdn.example/audio.mp4");
        resolver.ResolveAudioStreamUriAsync("vid22222222", Arg.Any<CancellationToken>()).Returns(streamUri);
        IMediaStreamPlayer mediaPlayer = Substitute.For<IMediaStreamPlayer>();

        await using AudioPlayerBackend backend = new(audioOut, resolver, mediaPlayer);
        Track track = new("vid22222222", new Uri("https://www.youtube.com/watch?v=vid22222222"), "Active Track", "Tobias", DateTimeOffset.UtcNow);

        await backend.PreparePausedAtPositionAsync(track, TimeSpan.FromSeconds(83.5));

        backend.CurrentState.Should().Be(PlaybackState.Paused);
        backend.ActiveTrack.Should().BeSameAs(track);
        mediaPlayer.Received(1).Open(streamUri);
        mediaPlayer.Received(1).Pause();
        mediaPlayer.Received(1).Seek(TimeSpan.FromSeconds(83.5));
    }
}

