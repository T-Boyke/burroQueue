using System.Text.Json;

namespace burroQueue.Infrastructure.Configuration;

public sealed class AppSettingsService : IAppSettingsService
{
    private readonly string _configFilePath;
    private AppSettingsData _currentSettings = new();

    public AppSettingsData CurrentSettings => _currentSettings;

    public AppSettingsService(string? customPath = null)
    {
        _configFilePath = customPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "burroQueue",
            "settings.json");
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_configFilePath))
        {
            _currentSettings = new AppSettingsData();
            return;
        }

        try
        {
            await using FileStream stream = File.OpenRead(_configFilePath);
            AppSettingsData? data = await JsonSerializer.DeserializeAsync<AppSettingsData>(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            _currentSettings = data ?? new AppSettingsData();
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
        string? directory = Path.GetDirectoryName(_configFilePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using FileStream stream = File.Create(_configFilePath);
        await JsonSerializer.SerializeAsync(stream, settings, cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
