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
            ClientDisplayName = "AliceInChains"
        };

        await service.SaveAsync(newSettings);

        AppSettingsService loader = new(_tempFile);
        await loader.LoadAsync();

        loader.CurrentSettings.SelectedTelephonyProviderId.Should().Be("WindowsCoreAudio");
        loader.CurrentSettings.SelectedAudioDeviceId.Should().Be("speaker-42");
        loader.CurrentSettings.CollaborativePlaylistUrl.Should().Be("https://youtube.com/playlist?list=XYZ&jct=123");
        loader.CurrentSettings.AutoOpenCollaborativePlaylistOnJoin.Should().BeFalse();
        loader.CurrentSettings.ClientDisplayName.Should().Be("AliceInChains");
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
