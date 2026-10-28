using System.Collections.ObjectModel;
using burroQueue.Domain.Model;
using burroQueue.Infrastructure.Audio;
using burroQueue.Infrastructure.Configuration;
using burroQueue.Infrastructure.Network;
using burroQueue.Infrastructure.Playback;
using burroQueue.Infrastructure.Telephony;
using burroQueue.Infrastructure.Web;
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
    private readonly BurroGroup? _group;
    private readonly ILanGroupService? _lanGroupService;
    private readonly IEmbeddedWebDropServer? _webDropServer;

    [ObservableProperty]
    private string _selectedProviderId;

    [ObservableProperty]
    private string _selectedAudioDeviceId = string.Empty;

    [ObservableProperty]
    private string _collaborativePlaylistUrl = string.Empty;

    [ObservableProperty]
    private bool _isCollaborativeUrlValid;

    [ObservableProperty]
    private string _collaborativeUrlValidationText = string.Empty;

    [ObservableProperty]
    private bool _autoOpenCollaborativePlaylistOnJoin;

    [ObservableProperty]
    private string _clientDisplayName;

    [ObservableProperty]
    private bool _isAutostartEnabled;

    [ObservableProperty]
    private bool _enableWebDropServer;

    [ObservableProperty]
    private string _currentGroupName = "Büro Jukebox";

    [ObservableProperty]
    private string _newGroupName = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private string _audioEngineStatus = "Invidious API Fallback";

    [ObservableProperty]
    private string _webDropEndpointInfo = string.Empty;

    [ObservableProperty]
    private string _lanMulticastInfo = "UDP Multicast 239.255.42.99:41234";

    public ObservableCollection<DiscoveredGroupItem> DiscoveredGroups { get; } = [];

    public IReadOnlyList<ITelephonyProvider> AvailableTelephonyProviders => _telephonyManager.AvailableProviders;

    public IReadOnlyList<AudioDeviceOption> AvailableAudioDevices { get; }

    public SettingsViewModel(
        IAppSettingsService settingsService,
        ITelephonyManager telephonyManager,
        IWindowsAutostartService autostartService,
        IAudioDeviceEnumerator audioDeviceEnumerator,
        BurroGroup? group = null,
        ILanGroupService? lanGroupService = null,
        IStreamUriResolver? streamResolver = null,
        IEmbeddedWebDropServer? webDropServer = null,
        ILocalNetworkInfoService? networkInfo = null)
    {
        _settingsService = settingsService;
        _telephonyManager = telephonyManager;
        _autostartService = autostartService;
        _audioDeviceEnumerator = audioDeviceEnumerator;
        _group = group;
        _lanGroupService = lanGroupService;
        _webDropServer = webDropServer;

        if (_group is not null)
        {
            _currentGroupName = _group.Name;
        }

        AppSettingsData current = _settingsService.CurrentSettings;
        _selectedProviderId = current.SelectedTelephonyProviderId;
        _selectedAudioDeviceId = current.SelectedAudioDeviceId;
        _collaborativePlaylistUrl = current.CollaborativePlaylistUrl ?? string.Empty;
        _autoOpenCollaborativePlaylistOnJoin = current.AutoOpenCollaborativePlaylistOnJoin;
        _clientDisplayName = current.ClientDisplayName;
        _isAutostartEnabled = _autostartService.IsAutostartEnabled;
        _enableWebDropServer = current.EnableWebDropServer;

        if (!string.IsNullOrWhiteSpace(_collaborativePlaylistUrl))
        {
            ValidateCollaborativeUrl(_collaborativePlaylistUrl);
        }

        AvailableAudioDevices = _audioDeviceEnumerator.GetAvailableRenderDevices();

        if (streamResolver is not null)
        {
            _audioEngineStatus = streamResolver.IsYtDlpAvailable
                ? "yt-dlp (Lokal/WinGet/PATH) – Volle Stream-Qualität"
                : "Invidious API Fallback – Cloud-Audio";
        }

        if (networkInfo is not null && _webDropServer is not null)
        {
            _webDropEndpointInfo = networkInfo.GetWebDropUrl(_webDropServer.Port);
        }

        if (_lanGroupService is not null)
        {
            _lanGroupService.GroupDiscovered += OnGroupDiscovered;
        }
    }

    private void OnGroupDiscovered(object? sender, DiscoveryBeaconMessage beacon)
    {
        DiscoveredGroupItem? existing = DiscoveredGroups.FirstOrDefault(g => g.GroupId == beacon.GroupId);
        if (existing is not null)
        {
            int index = DiscoveredGroups.IndexOf(existing);
            DiscoveredGroups[index] = new DiscoveredGroupItem(beacon.GroupId, beacon.GroupName, beacon.MemberCount, beacon.HostEndpoint, DateTimeOffset.UtcNow);
        }
        else
        {
            DiscoveredGroups.Add(new DiscoveredGroupItem(beacon.GroupId, beacon.GroupName, beacon.MemberCount, beacon.HostEndpoint, DateTimeOffset.UtcNow));
        }
    }

    partial void OnCollaborativePlaylistUrlChanged(string value)
    {
        ValidateCollaborativeUrl(value);
    }

    private void ValidateCollaborativeUrl(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            IsCollaborativeUrlValid = false;
            CollaborativeUrlValidationText = string.Empty;
            return;
        }

        try
        {
            CollaborativePlaylistLink link = new(value);
            IsCollaborativeUrlValid = true;
            CollaborativeUrlValidationText = $"✓ Gültige Playlist ({link.PlaylistId}) mit Kollaborationstoken erkannt!";
        }
        catch
        {
            IsCollaborativeUrlValid = false;
            CollaborativeUrlValidationText = "⚠️ Ungültiger Link – ein YouTube-Playlistlink mit &jct=... ist erforderlich.";
        }
    }

    [RelayCommand]
    public async Task ScanGroupsAsync()
    {
        if (_lanGroupService is not null && _group is not null)
        {
            await _lanGroupService.BroadcastBeaconAsync(_group).ConfigureAwait(false);
            StatusMessage = "Suche nach Jukebox-Gruppen im Netzwerk...";
        }
    }

    [RelayCommand]
    public async Task JoinGroupAsync(DiscoveredGroupItem? target)
    {
        if (target is null || _group is null)
        {
            return;
        }

        _group.SwitchGroup(new GroupId(target.GroupId), target.GroupName);
        CurrentGroupName = target.GroupName;
        if (_lanGroupService is not null)
        {
            await _lanGroupService.SendGroupActionAsync(new GroupId(target.GroupId), "Join", ClientDisplayName).ConfigureAwait(false);
        }

        StatusMessage = $"Gruppe '{target.GroupName}' beigetreten!";
    }

    [RelayCommand]
    public void CreateNewGroup()
    {
        if (string.IsNullOrWhiteSpace(NewGroupName) || _group is null)
        {
            return;
        }

        string trimmed = NewGroupName.Trim();
        _group.SwitchGroup(GroupId.New(), trimmed);
        CurrentGroupName = trimmed;
        NewGroupName = string.Empty;
        StatusMessage = $"Neue Gruppe '{trimmed}' gegründet!";
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
            ClientDisplayName = string.IsNullOrWhiteSpace(ClientDisplayName) ? Environment.UserName : ClientDisplayName.Trim(),
            EnableWebDropServer = EnableWebDropServer
        };

        await _settingsService.SaveAsync(data).ConfigureAwait(false);
        await _telephonyManager.SwitchProviderAsync(SelectedProviderId).ConfigureAwait(false);
        _autostartService.SetAutostart(IsAutostartEnabled);

        if (_webDropServer is not null)
        {
            if (EnableWebDropServer && !_webDropServer.IsRunning)
            {
                await _webDropServer.StartAsync().ConfigureAwait(false);
            }
            else if (!EnableWebDropServer && _webDropServer.IsRunning)
            {
                await _webDropServer.StopAsync().ConfigureAwait(false);
            }
        }

        StatusMessage = "Einstellungen gespeichert!";
    }
}
