using burroQueue.Domain.Model;
using burroQueue.Infrastructure.Configuration;
using burroQueue.Infrastructure.Telephony;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace burroQueue.Presentation.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly IAppSettingsService _settingsService;
    private readonly ITelephonyManager _telephonyManager;

    [ObservableProperty]
    private string _selectedProviderId;

    [ObservableProperty]
    private string _collaborativePlaylistUrl = string.Empty;

    [ObservableProperty]
    private bool _autoOpenCollaborativePlaylistOnJoin;

    [ObservableProperty]
    private string _clientDisplayName;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public IReadOnlyList<ITelephonyProvider> AvailableTelephonyProviders => _telephonyManager.AvailableProviders;

    public SettingsViewModel(IAppSettingsService settingsService, ITelephonyManager telephonyManager)
    {
        _settingsService = settingsService;
        _telephonyManager = telephonyManager;

        AppSettingsData current = _settingsService.CurrentSettings;
        _selectedProviderId = current.SelectedTelephonyProviderId;
        _collaborativePlaylistUrl = current.CollaborativePlaylistUrl ?? string.Empty;
        _autoOpenCollaborativePlaylistOnJoin = current.AutoOpenCollaborativePlaylistOnJoin;
        _clientDisplayName = current.ClientDisplayName;
    }

    [RelayCommand]
    public async Task SaveSettingsAsync()
    {
        if (!string.IsNullOrWhiteSpace(CollaborativePlaylistUrl))
        {
            try
            {
                _ = new CollaborativePlaylistLink(CollaborativePlaylistUrl);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Ungültiger Link: {ex.Message}";
                return;
            }
        }

        AppSettingsData data = new()
        {
            SelectedTelephonyProviderId = SelectedProviderId,
            CollaborativePlaylistUrl = string.IsNullOrWhiteSpace(CollaborativePlaylistUrl) ? null : CollaborativePlaylistUrl.Trim(),
            AutoOpenCollaborativePlaylistOnJoin = AutoOpenCollaborativePlaylistOnJoin,
            ClientDisplayName = string.IsNullOrWhiteSpace(ClientDisplayName) ? Environment.UserName : ClientDisplayName.Trim()
        };

        await _settingsService.SaveAsync(data).ConfigureAwait(false);
        await _telephonyManager.SwitchProviderAsync(SelectedProviderId).ConfigureAwait(false);

        StatusMessage = "Einstellungen gespeichert!";
    }
}

