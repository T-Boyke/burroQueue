namespace burroQueue.Infrastructure.Configuration;

public interface IAppSettingsService
{
    AppSettingsData CurrentSettings { get; }

    Task LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(AppSettingsData settings, CancellationToken cancellationToken = default);
}

