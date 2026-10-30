using System.Text.Json;
using burroQueue.Infrastructure.Configuration;
using FluentAssertions;
using Xunit;

namespace burroQueue.Tests.Infrastructure;

public sealed class AppSettingsServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _tempFile;

    public AppSettingsServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "burroQueueTests_" + Guid.NewGuid().ToString("N"));
        _tempFile = Path.Combine(_tempDir, "settings.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            try
            {
                Directory.Delete(_tempDir, true);
            }
            catch
            {
                // Best effort cleanup
            }
        }
    }

    [Fact]
    public async Task LoadAsync_WhenFileDoesNotExist_ShouldUseDefaultSettings()
    {
        AppSettingsService service = new(_tempFile);

        await service.LoadAsync();

        service.CurrentSettings.Should().NotBeNull();
        service.CurrentSettings.SelectedTelephonyProviderId.Should().Be("SwyxIt");
        service.CurrentSettings.ClientDisplayName.Should().NotBeNullOrWhiteSpace();
        service.CurrentSettings.MasterVolume.Should().Be(0.8);
        service.CurrentSettings.DuckingReleaseDelaySeconds.Should().Be(3);
        service.CurrentSettings.AutoSyncCollaborativePlaylist.Should().BeTrue();
        service.CurrentSettings.AutoSyncPlaylistIntervalSeconds.Should().Be(60);
        service.CurrentSettings.WindowBackdrop.Should().Be("Mica");
        service.CurrentSettings.UseSystemAccentColor.Should().BeTrue();
        service.CurrentSettings.EnableLoudnessNormalization.Should().BeTrue();
        service.CurrentSettings.TargetLoudnessLufs.Should().Be(-14.0);
    }

    [Fact]
    public async Task SaveAsync_ThenLoadAsync_InTomlMode_ShouldPersistLoudnessNormalizationSettings()
    {
        string tomlFile = Path.Combine(_tempDir, "settings.toml");
        AppSettingsService service = new(tomlFile);
        AppSettingsData newSettings = new()
        {
            EnableLoudnessNormalization = true,
            TargetLoudnessLufs = -16.5,
            MasterVolume = 0.75
        };

        await service.SaveAsync(newSettings);

        AppSettingsService loader = new(tomlFile);
        await loader.LoadAsync();

        loader.CurrentSettings.EnableLoudnessNormalization.Should().BeTrue();
        loader.CurrentSettings.TargetLoudnessLufs.Should().Be(-16.5);
        loader.CurrentSettings.MasterVolume.Should().Be(0.75);
    }

    [Fact]
    public async Task SaveAsync_ThenLoadAsync_ShouldPersistAndRestoreSettings()
    {
        AppSettingsService service = new(_tempFile);
        AppSettingsData newSettings = new()
        {
            SelectedTelephonyProviderId = "WindowsCoreAudio",
            SelectedAudioDeviceId = "speaker-42",
            CollaborativePlaylistUrl = "https://youtube.com/playlist?list=XYZ&jct=123",
            AutoOpenCollaborativePlaylistOnJoin = false,
            ClientDisplayName = "AliceInChains",
            MasterVolume = 0.45
        };

        await service.SaveAsync(newSettings);

        AppSettingsService loader = new(_tempFile);
        await loader.LoadAsync();

        loader.CurrentSettings.SelectedTelephonyProviderId.Should().Be("WindowsCoreAudio");
        loader.CurrentSettings.SelectedAudioDeviceId.Should().Be("speaker-42");
        loader.CurrentSettings.CollaborativePlaylistUrl.Should().Be("https://youtube.com/playlist?list=XYZ&jct=123");
        loader.CurrentSettings.AutoOpenCollaborativePlaylistOnJoin.Should().BeFalse();
        loader.CurrentSettings.ClientDisplayName.Should().Be("AliceInChains");
        loader.CurrentSettings.MasterVolume.Should().Be(0.45);
    }

    [Fact]
    public async Task LoadAsync_WhenFileCorrupt_ShouldGracefullyFallbackToDefaultSettings()
    {
        Directory.CreateDirectory(_tempDir);
        await File.WriteAllTextAsync(_tempFile, "{ broken-json: true,");

        AppSettingsService service = new(_tempFile);
        await service.LoadAsync();

        service.CurrentSettings.Should().NotBeNull();
        service.CurrentSettings.SelectedTelephonyProviderId.Should().Be("SwyxIt");
    }

    [Fact]
    public async Task SaveAsync_WhenNullSettings_ShouldThrowArgumentNullException()
    {
        AppSettingsService service = new(_tempFile);

        Func<Task> act = async () => await service.SaveAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
