using burroQueue.Domain.Model;
using burroQueue.Infrastructure.Audio;
using burroQueue.Infrastructure.Configuration;
using burroQueue.Infrastructure.Telephony;
using burroQueue.Infrastructure.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace burroQueue.Presentation.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly IAppSettingsService _settingsService;
    private readonly ITelephonyManager _telephonyManager;
    private readonly IWindowsAutostartService _autostartService;
    private readonly IAudioDeviceEnumerator _audioDeviceEnumerator;

    [ObservableProperty]
    private string _selectedProviderId;

    [ObservableProperty]
    private string _selectedAudioDeviceId = string.Empty;

    [ObservableProperty]
    private string _collaborativePlaylistUrl = string.Empty;

    [ObservableProperty]
    private bool _autoOpenCollaborativePlaylistOnJoin;

    [ObservableProperty]
    private string _clientDisplayName;

    [ObservableProperty]
    private bool _isAutostartEnabled;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public IReadOnlyList<ITelephonyProvider> AvailableTelephonyProviders => _telephonyManager.AvailableProviders;

    public IReadOnlyList<AudioDeviceOption> AvailableAudioDevices { get; }

    public SettingsViewModel(
        IAppSettingsService settingsService,
        ITelephonyManager telephonyManager,
        IWindowsAutostartService autostartService,
        IAudioDeviceEnumerator audioDeviceEnumerator)
    {
        _settingsService = settingsService;
        _telephonyManager = telephonyManager;
        _autostartService = autostartService;
        _audioDeviceEnumerator = audioDeviceEnumerator;

        AppSettingsData current = _settingsService.CurrentSettings;
        _selectedProviderId = current.SelectedTelephonyProviderId;
        _selectedAudioDeviceId = current.SelectedAudioDeviceId;
        _collaborativePlaylistUrl = current.CollaborativePlaylistUrl ?? string.Empty;
        _autoOpenCollaborativePlaylistOnJoin = current.AutoOpenCollaborativePlaylistOnJoin;
        _clientDisplayName = current.ClientDisplayName;
        _isAutostartEnabled = _autostartService.IsAutostartEnabled;

        AvailableAudioDevices = _audioDeviceEnumerator.GetAvailableRenderDevices();
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
            SelectedAudioDeviceId = SelectedAudioDeviceId,
            CollaborativePlaylistUrl = string.IsNullOrWhiteSpace(CollaborativePlaylistUrl) ? null : CollaborativePlaylistUrl.Trim(),
            AutoOpenCollaborativePlaylistOnJoin = AutoOpenCollaborativePlaylistOnJoin,
            ClientDisplayName = string.IsNullOrWhiteSpace(ClientDisplayName) ? Environment.UserName : ClientDisplayName.Trim()
        };

        await _settingsService.SaveAsync(data).ConfigureAwait(false);
        await _telephonyManager.SwitchProviderAsync(SelectedProviderId).ConfigureAwait(false);
        _autostartService.SetAutostart(IsAutostartEnabled);

        StatusMessage = "Einstellungen gespeichert!";
    }
}
