using System.Collections.ObjectModel;
using System.Diagnostics;
using burroQueue.Domain.Model;
using burroQueue.Infrastructure.Audio;
using burroQueue.Infrastructure.Configuration;
using burroQueue.Infrastructure.Diagnostics;
using burroQueue.Infrastructure.Network;
using burroQueue.Infrastructure.Network.Messages;
using burroQueue.Infrastructure.Playback;
using burroQueue.Infrastructure.Telephony;
using burroQueue.Infrastructure.Web;
using burroQueue.Infrastructure.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace burroQueue.Presentation.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private const string YtDlpRepoUrl = "https://github.com/yt-dlp/yt-dlp";
    private const string AuthorProfileUrl = "https://github.com/T-Boyke";
    private const string ProjectRepoUrl = "https://github.com/T-Boyke/burroQueue";
    private const string BugReportUrl = "https://github.com/T-Boyke/burroQueue/issues/new?template=bug_report.yml";
    private const string FeatureRequestUrl = "https://github.com/T-Boyke/burroQueue/issues/new?template=feature_request.yml";

    internal static Action<ProcessStartInfo> ProcessStarter { get; set; } = psi => Process.Start(psi);

    private readonly IAppSettingsService _settingsService;
    private readonly ITelephonyManager _telephonyManager;
    private readonly IWindowsAutostartService _autostartService;
    private readonly IAudioDeviceEnumerator _audioDeviceEnumerator;
    private readonly IStreamUriResolver? _streamResolver;
    private readonly IGitHubRepoStatsClient? _gitHubStatsClient;
    private readonly ILocalNetworkInfoService? _networkInfo;
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
    private int _duckingReleaseDelaySeconds = 3;

    [ObservableProperty]
    private bool _autoSyncCollaborativePlaylist = true;

    [ObservableProperty]
    private int _autoSyncPlaylistIntervalSeconds = 60;

    [ObservableProperty]
    private string _selectedWindowBackdrop = "Mica";

    [ObservableProperty]
    private bool _useSystemAccentColor = true;

    public IReadOnlyList<string> AvailableWindowBackdrops { get; } =
    [
        "Auto",
        "Mica",
        "Tabbed",
        "Acrylic",
        "None"
    ];

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

    [ObservableProperty]
    private string _techStackSummary = ".NET 11 (C# 15) · Native WPF Fluent UI · UnoCSS 66.12.0 (0 Bootstrap)";

    [ObservableProperty]
    private string _ytDlpVersionText = "yt-dlp (Invidious Cloud-Fallback)";

    [ObservableProperty]
    private string _authorDisplayName = "Tobias Boyke";

    [ObservableProperty]
    private string _authorHandle = "@T-Boyke";

    [ObservableProperty]
    private Uri _authorAvatarUri = new("https://avatars.githubusercontent.com/u/37149164?v=4&s=72");

    [ObservableProperty]
    private string _authorStatsSummary = "17 Repos · 24 Followers · ★ 76 · AGPL-3.0";

    [ObservableProperty]
    private string _appVersionBadge = "v1.1.0";

    private string _authorProfileUrl = AuthorProfileUrl;
    private string _projectRepoUrl = ProjectRepoUrl;

    internal Task AboutLoadTask { get; private set; } = Task.CompletedTask;

    public ObservableCollection<DiscoveredGroupItem> DiscoveredGroups { get; } = [];

    public IReadOnlyList<ITelephonyProvider> AvailableTelephonyProviders => _telephonyManager.AvailableProviders;

    public bool IsStarfaceSelected => string.Equals(SelectedProviderId, "Starface", StringComparison.OrdinalIgnoreCase);

    public IReadOnlyList<AudioDeviceOption> AvailableAudioDevices { get; }

    public SettingsViewModel(
        IAppSettingsService settingsService,
        ITelephonyManager telephonyManager,
        IWindowsAutostartService autostartService,
        IAudioDeviceEnumerator audioDeviceEnumerator,
        IStreamUriResolver? streamResolver = null,
        IEmbeddedWebDropServer? webDropServer = null,
        ILocalNetworkInfoService? networkInfo = null,
        BurroGroup? group = null,
        ILanGroupService? lanGroupService = null,
        IGitHubRepoStatsClient? gitHubStatsClient = null)
    {
        _settingsService = settingsService;
        _telephonyManager = telephonyManager;
        _autostartService = autostartService;
        _audioDeviceEnumerator = audioDeviceEnumerator;
        _streamResolver = streamResolver;
        _gitHubStatsClient = gitHubStatsClient;
        _networkInfo = networkInfo;
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
        _duckingReleaseDelaySeconds = Math.Clamp(current.DuckingReleaseDelaySeconds, 0, 120);
        _autoSyncCollaborativePlaylist = current.AutoSyncCollaborativePlaylist;
        _autoSyncPlaylistIntervalSeconds = Math.Clamp(current.AutoSyncPlaylistIntervalSeconds, 15, 3600);
        _selectedWindowBackdrop = string.IsNullOrWhiteSpace(current.WindowBackdrop) ? "Mica" : current.WindowBackdrop;
        _useSystemAccentColor = current.UseSystemAccentColor;

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

        AboutLoadTask = LoadAboutMetadataAsync();
    }

    private async Task LoadAboutMetadataAsync()
    {
        if (_streamResolver is not null)
        {
            if (_streamResolver.IsYtDlpAvailable)
            {
                string? ver = await _streamResolver.GetYtDlpVersionAsync().ConfigureAwait(false);
                YtDlpVersionText = string.IsNullOrWhiteSpace(ver)
                    ? "yt-dlp (Lokal installiert)"
                    : $"yt-dlp {ver}";
            }
            else
            {
                YtDlpVersionText = "Nicht lokal installiert (Invidious Cloud-Fallback)";
            }
        }

        if (_gitHubStatsClient is not null)
        {
            GitHubAboutSnapshot snap = await _gitHubStatsClient.FetchSnapshotAsync().ConfigureAwait(false);
            AuthorDisplayName = snap.AuthorDisplayName;
            AuthorHandle = snap.AuthorHandle;
            AuthorAvatarUri = snap.AuthorAvatarUri;
            _authorProfileUrl = snap.AuthorProfileUri.ToString();
            _projectRepoUrl = snap.RepositoryUri.ToString();
            AppVersionBadge = $"v{snap.AppVersion}";
            AuthorStatsSummary = $"{snap.PublicRepos} Repos · {snap.Followers} Followers · ★ {snap.Stars} · {snap.LicenseSpdx}";
        }
    }

    [RelayCommand]
    public void OpenYtDlpRepository() => OpenExternalUrl(YtDlpRepoUrl);

    [RelayCommand]
    public void OpenAuthorProfile() => OpenExternalUrl(_authorProfileUrl);

    [RelayCommand]
    public void OpenProjectRepository() => OpenExternalUrl(_projectRepoUrl);

    [RelayCommand]
    public void ReportBugOnGitHub() => OpenExternalUrl(BugReportUrl);

    [RelayCommand]
    public void RequestFeatureOnGitHub() => OpenExternalUrl(FeatureRequestUrl);

    private void OpenExternalUrl(string url)
    {
        try
        {
            ProcessStarter(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch
        {
            StatusMessage = "Browser konnte nicht geöffnet werden.";
        }
    }

    private void OnGroupDiscovered(object? sender, DiscoveryBeaconMessage beacon)
    {
        DiscoveredGroupItem? existing = DiscoveredGroups.FirstOrDefault(g => g.GroupId == beacon.GroupId);
        if (existing is not null)
        {
            int index = DiscoveredGroups.IndexOf(existing);
            DiscoveredGroups[index] = new DiscoveredGroupItem(beacon.GroupId, beacon.GroupName, beacon.MemberCount, beacon.HostDisplayName, DateTimeOffset.UtcNow);
        }
        else
        {
            DiscoveredGroups.Add(new DiscoveredGroupItem(beacon.GroupId, beacon.GroupName, beacon.MemberCount, beacon.HostDisplayName, DateTimeOffset.UtcNow));
        }
    }

    internal Task WebDropToggleTask { get; private set; } = Task.CompletedTask;

    partial void OnEnableWebDropServerChanged(bool value)
    {
        if (_webDropServer is null)
        {
            return;
        }

        WebDropToggleTask = ToggleWebDropServerImmediatelyAsync(value);
    }

    private async Task ToggleWebDropServerImmediatelyAsync(bool enable)
    {
        if (_webDropServer is null)
        {
            return;
        }

        try
        {
            if (enable && !_webDropServer.IsRunning)
            {
                await _webDropServer.StartAsync().ConfigureAwait(false);
            }
            else if (!enable && _webDropServer.IsRunning)
            {
                await _webDropServer.StopAsync().ConfigureAwait(false);
            }

            if (_networkInfo is not null)
            {
                WebDropEndpointInfo = _networkInfo.GetWebDropUrl(_webDropServer.Port);
            }

            _settingsService.CurrentSettings.EnableWebDropServer = enable;
            await _settingsService.SaveAsync(_settingsService.CurrentSettings).ConfigureAwait(false);
        }
        catch
        {
            // Fail-safe
        }
    }

    partial void OnCollaborativePlaylistUrlChanged(string value)
    {
        ValidateCollaborativeUrl(value);
    }

    partial void OnSelectedProviderIdChanged(string value)
    {
        OnPropertyChanged(nameof(IsStarfaceSelected));
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
            await _lanGroupService.BroadcastBeaconAsync(_group, ClientId.New(), ClientDisplayName).ConfigureAwait(false);
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
            await _lanGroupService.SendGroupActionAsync(new GroupId(target.GroupId), ClientId.New(), "Join", ClientDisplayName).ConfigureAwait(false);
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
            EnableWebDropServer = EnableWebDropServer,
            MasterVolume = _settingsService.CurrentSettings.MasterVolume,
            HasJoinedCollaborativePlaylist = _settingsService.CurrentSettings.HasJoinedCollaborativePlaylist,
            DuckingReleaseDelaySeconds = Math.Clamp(DuckingReleaseDelaySeconds, 0, 120),
            AutoSyncCollaborativePlaylist = AutoSyncCollaborativePlaylist,
            AutoSyncPlaylistIntervalSeconds = Math.Clamp(AutoSyncPlaylistIntervalSeconds, 15, 3600),
            WindowBackdrop = string.IsNullOrWhiteSpace(SelectedWindowBackdrop) ? "Mica" : SelectedWindowBackdrop,
            UseSystemAccentColor = UseSystemAccentColor
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

            if (_networkInfo is not null)
            {
                WebDropEndpointInfo = _networkInfo.GetWebDropUrl(_webDropServer.Port);
            }
        }

        StatusMessage = "Einstellungen gespeichert!";
    }
}
