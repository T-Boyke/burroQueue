using System.Collections.ObjectModel;
using System.Diagnostics;
using burroQueue.Domain.Model;
using burroQueue.Infrastructure.Audio;
using burroQueue.Infrastructure.Configuration;
using burroQueue.Infrastructure.Network;
using burroQueue.Infrastructure.Network.Messages;
using burroQueue.Infrastructure.Persistence;
using burroQueue.Infrastructure.Playback;
using burroQueue.Infrastructure.Telephony;
using burroQueue.Infrastructure.Web;
using burroQueue.Infrastructure.YouTube;
using burroQueue.Presentation.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace burroQueue.Presentation.ViewModels;

public sealed partial class MainWindowViewModel : ObservableObject, IDisposable
{
    private readonly BurroGroup _group;
    private readonly IYouTubeUrlParser _urlParser;
    private readonly IYouTubeMetadataFetcher _metadataFetcher;
    private readonly ITelephonyManager _telephonyManager;
    private readonly ILanGroupService _lanGroupService;
    private readonly IAudioOutputController _audioController;
    private readonly IAppSettingsService _settingsService;
    private readonly IEmbeddedWebDropServer _webDropServer;
    private readonly IAudioPlayerBackend _audioPlayerBackend;
    private readonly IPlaylistStorageService _playlistStorage;
    private readonly IAudioDeviceEnumerator? _audioDeviceEnumerator;
    private readonly ISystrayNotificationService? _systrayService;
    private readonly IQrCodeSvgGenerator? _qrCodeGenerator;
    private readonly IYouTubePlaylistReader? _playlistReader;
    private readonly IYouTubePlaylistWriter? _playlistWriter;
    private readonly ClientId _localClientId = ClientId.New();
    private SkipVotePool _skipVotePool;
    private bool _isUpdatingVolumeInternally;
    private double _lastUnmutedVolume = 0.8;
    private CancellationTokenSource? _feedbackCts;
    private CancellationTokenSource? _duckingReleaseCts;
    private CancellationTokenSource? _playlistAutoSyncCts;
    private readonly System.Windows.Threading.DispatcherTimer? _playbackTimer;

    internal static Func<TimeSpan, CancellationToken, Task> DelayProvider { get; set; } = Task.Delay;


    public event EventHandler? OpenSettingsRequested;

    internal static Action<string> ClipboardAction { get; set; } = System.Windows.Clipboard.SetText;
    internal static Action<ProcessStartInfo> ProcessStarter { get; set; } = psi => Process.Start(psi);
    internal static Action<Action> UiDispatcherRunner { get; set; } = InvokeOnDispatcher;

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    internal static void InvokeOnDispatcher(Action action)
    {
        if (System.Windows.Application.Current?.Dispatcher is { HasShutdownStarted: false } dispatcher && !dispatcher.CheckAccess())
        {
            try
            {
                dispatcher.Invoke(action);
                return;
            }
            catch
            {
                // Fallback bei Dispatcher-Shutdown
            }
        }

        action();
    }

    [ObservableProperty]
    private string _groupName;

    [ObservableProperty]
    private string _statusMessage = "Drop a YouTube link here";

    [ObservableProperty]
    private int _queuedCount;

    [ObservableProperty]
    private PlaybackState _playbackState = PlaybackState.Stopped;

    [ObservableProperty]
    private bool _isCallActive;

    [ObservableProperty]
    private string _telephonyStatusText = string.Empty;

    [ObservableProperty]
    private string _telephonyProviderName = "SwyxIt!";

    [ObservableProperty]
    private string _telephonyStateSummary = "Bereit (Überwachung aktiv)";

    [ObservableProperty]
    private double _masterVolume = 0.8;

    [ObservableProperty]
    private bool _isMuted;

    [ObservableProperty]
    private bool _isAudioDucked;

    [ObservableProperty]
    private string _activeAudioDeviceName = "Standard-Ausgabegerät";

    [ObservableProperty]
    private bool _isQrCodeVisible;

    [ObservableProperty]
    private string _qrCodeSvg = string.Empty;

    [ObservableProperty]
    private System.Windows.Media.ImageSource? _qrCodeImage;

    partial void OnMasterVolumeChanged(double value)
    {
        if (!_isUpdatingVolumeInternally)
        {
            _audioController.SetTargetVolume(value, _isAudioDucked);
            if (value > 0)
            {
                _lastUnmutedVolume = value;
                IsMuted = false;
            }

            _settingsService.CurrentSettings.MasterVolume = value;
            AutoSaveSettings();
        }
    }

    [ObservableProperty]
    private string? _collaborativePlaylistUrl;

    [ObservableProperty]
    private string _currentTrackTitle = "No song playing";

    [ObservableProperty]
    private string _currentTrackArtist = string.Empty;

    [ObservableProperty]
    private string? _currentTrackThumbnailUrl;

    [ObservableProperty]
    private string _skipVoteStatus = "0 / 3 Votes";

    [ObservableProperty]
    private double _playbackProgress;

    [ObservableProperty]
    private string _playbackTimeFormatted = "00:00 / 00:00";

    [ObservableProperty]
    private string _webDropUrl = "http://localhost:8080";

    [ObservableProperty]
    private string _quickAddUrl = string.Empty;

    [ObservableProperty]
    private bool _isQueueExpanded;

    [ObservableProperty]
    private bool _isAddFeedbackVisible;

    [ObservableProperty]
    private string _lastAddedFeedbackText = string.Empty;

    [ObservableProperty]
    private bool _isFirstJoinPromptVisible;

    [ObservableProperty]
    private bool _isWebDropActive;

    partial void OnIsWebDropActiveChanged(bool value)
    {
        NetworkStatusText = value ? "LAN-Sync & WebDrop aktiv" : "LAN-Sync aktiv (WebDrop aus)";
    }

    [ObservableProperty]
    private string _networkStatusText = "LAN-Sync & WebDrop aktiv";

    public ObservableCollection<Track> QueuedItems { get; } = [];

    internal Task AutoSaveTask { get; private set; } = Task.CompletedTask;

    internal Task AutoSaveSettingsTask { get; private set; } = Task.CompletedTask;

    internal Task MetadataLoadTask { get; private set; } = Task.CompletedTask;

    internal Task PlaybackCommandTask { get; private set; } = Task.CompletedTask;

    public MainWindowViewModel(
        BurroGroup group,
        IYouTubeUrlParser urlParser,
        IYouTubeMetadataFetcher metadataFetcher,
        ITelephonyManager telephonyManager,
        ILanGroupService lanGroupService,
        IAudioOutputController audioController,
        IAppSettingsService settingsService,
        IEmbeddedWebDropServer webDropServer,
        ILocalNetworkInfoService networkInfoService,
        IAudioPlayerBackend audioPlayerBackend,
        IPlaylistStorageService playlistStorage,
        IAudioDeviceEnumerator? audioDeviceEnumerator = null,
        ISystrayNotificationService? systrayService = null,
        IQrCodeSvgGenerator? qrCodeGenerator = null,
        IYouTubePlaylistReader? playlistReader = null,
        IYouTubePlaylistWriter? playlistWriter = null)
    {
        _group = group ?? throw new ArgumentNullException(nameof(group));
        _urlParser = urlParser ?? throw new ArgumentNullException(nameof(urlParser));
        _metadataFetcher = metadataFetcher ?? throw new ArgumentNullException(nameof(metadataFetcher));
        _telephonyManager = telephonyManager ?? throw new ArgumentNullException(nameof(telephonyManager));
        _lanGroupService = lanGroupService ?? throw new ArgumentNullException(nameof(lanGroupService));
        _audioController = audioController ?? throw new ArgumentNullException(nameof(audioController));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _webDropServer = webDropServer ?? throw new ArgumentNullException(nameof(webDropServer));
        _audioPlayerBackend = audioPlayerBackend ?? throw new ArgumentNullException(nameof(audioPlayerBackend));
        _playlistStorage = playlistStorage ?? throw new ArgumentNullException(nameof(playlistStorage));
        _audioDeviceEnumerator = audioDeviceEnumerator;
        _systrayService = systrayService;
        _qrCodeGenerator = qrCodeGenerator;
        _playlistReader = playlistReader;
        _playlistWriter = playlistWriter;
        ArgumentNullException.ThrowIfNull(networkInfoService);

        _groupName = _group.Name;
        _group.Renamed += OnGroupRenamed;
        _collaborativePlaylistUrl = _settingsService.CurrentSettings.CollaborativePlaylistUrl;
        _webDropUrl = networkInfoService.GetWebDropUrl(8080);
        _skipVotePool = new SkipVotePool(TrackId.New(), requiredVotes: 3);
        _isWebDropActive = _settingsService.CurrentSettings.EnableWebDropServer;
        _networkStatusText = _isWebDropActive ? "LAN-Sync & WebDrop aktiv" : "LAN-Sync aktiv (WebDrop aus)";
        _isFirstJoinPromptVisible = !string.IsNullOrWhiteSpace(_settingsService.CurrentSettings.CollaborativePlaylistUrl) && !_settingsService.CurrentSettings.HasJoinedCollaborativePlaylist;

        if (_settingsService.CurrentSettings.MasterVolume > 0 && Math.Abs(_settingsService.CurrentSettings.MasterVolume - 0.8) > 0.001)
        {
            _masterVolume = _settingsService.CurrentSettings.MasterVolume;
        }
        else
        {
            _masterVolume = _audioController.CurrentVolume;
        }

        if (Math.Abs(_masterVolume - _audioController.CurrentVolume) > 0.001)
        {
            _audioController.SetTargetVolume(_masterVolume, _audioController.IsDucked);
        }

        _lastUnmutedVolume = _masterVolume > 0 ? _masterVolume : 0.8;
        _isAudioDucked = _audioController.IsDucked;

        if (_telephonyManager.ActiveProvider is { } activeProvider)
        {
            _telephonyProviderName = activeProvider.DisplayName;
        }

        _audioDeviceEnumerator = audioDeviceEnumerator;
        if (_audioDeviceEnumerator is not null && !string.IsNullOrEmpty(_settingsService.CurrentSettings.SelectedAudioDeviceId))
        {
            AudioDeviceOption? device = _audioDeviceEnumerator.GetAvailableRenderDevices()
                .FirstOrDefault(d => d.Id == _settingsService.CurrentSettings.SelectedAudioDeviceId);
            if (device is not null)
            {
                _activeAudioDeviceName = device.DisplayName;
            }
        }

        _audioController.VolumeChanged += (_, vol) =>
        {
            RunOnUi(() =>
            {
                _isUpdatingVolumeInternally = true;
                MasterVolume = vol;
                _isUpdatingVolumeInternally = false;
            });
        };

        SyncQueuedItems();
        if (_group.Playlist.CurrentTrack is { } initialTrack)
        {
            _currentTrackTitle = initialTrack.Title;
            _currentTrackArtist = initialTrack.SubmittedBy;
            _currentTrackThumbnailUrl = initialTrack.ThumbnailUrl;
        }

        _telephonyManager.StateChanged += OnTelephonyStateChanged;
        _lanGroupService.GroupActionReceived += OnGroupActionReceived;
        _lanGroupService.PlaybackCommandReceived += OnPlaybackCommandReceived;
        _lanGroupService.CollaborativePlaylistReceived += OnCollaborativePlaylistReceived;
        _lanGroupService.QueueSyncReceived += OnQueueSyncReceived;
        _lanGroupService.SkipVoteReceived += OnSkipVoteReceived;
        _audioPlayerBackend.TrackPlaybackFinished += OnTrackPlaybackFinished;

        if (System.Windows.Application.Current?.Dispatcher is { HasShutdownStarted: false } dispatcher)
        {
            try
            {
                _playbackTimer = new System.Windows.Threading.DispatcherTimer(
                    TimeSpan.FromSeconds(1),
                    System.Windows.Threading.DispatcherPriority.Background,
                    OnPlaybackTimerTick,
                    dispatcher);
                _playbackTimer.Stop();
            }
            catch
            {
                // Fail-safe bei Test-Runnern ohne aktiven UI-Dispatcher
            }
        }
        _webDropServer.LinkDropped += (_, req) => EnqueueRawUrl(req.Url, req.Submitter);
        _webDropServer.SkipVoteRequested += async (_, _) => await VoteToSkipAsync().ConfigureAwait(false);
        _webDropServer.SkipVoteStatusProvider = () => SkipVoteStatus;
    }

    public async Task InitializeAsync()
    {
        try
        {
            await _playlistStorage.LoadPlaylistAsync(_group.Playlist).ConfigureAwait(false);
            RunOnUi(SyncQueuedItems);
        }
        catch
        {
            // Fail-safe: Fallback auf leere Playlist
        }

        if (_group.Playlist.Count == 0 && !string.IsNullOrWhiteSpace(CollaborativePlaylistUrl))
        {
            await ImportPlaylistFromYouTubeAsync(CollaborativePlaylistUrl).ConfigureAwait(false);
        }

        ConfigurePlaylistAutoSyncLoop();
    }

    private void AutoSavePlaylist()
    {
        AutoSaveTask = Task.Run(async () =>
        {
            try
            {
                await _playlistStorage.SavePlaylistAsync(_group.Playlist).ConfigureAwait(false);
            }
            catch
            {
                // Auto-Save ist fail-safe und blockiert die App nicht
            }
        });
    }

    private void AutoSaveSettings()
    {
        AutoSaveSettingsTask = Task.Run(async () =>
        {
            try
            {
                await _settingsService.SaveAsync(_settingsService.CurrentSettings).ConfigureAwait(false);
            }
            catch
            {
                // Fail-safe
            }
        });
    }

    [RelayCommand]
    public void ToggleQrCode()
    {
        IsQrCodeVisible = !IsQrCodeVisible;
        if (IsQrCodeVisible && string.IsNullOrEmpty(QrCodeSvg))
        {
            QrCodeSvg = _qrCodeGenerator?.GenerateSvg(WebDropUrl, 250) ?? string.Empty;
        }

        if (IsQrCodeVisible && QrCodeImage is null && _qrCodeGenerator is not null)
        {
            QrCodeImage = GenerateQrCodeBitmap(WebDropUrl);
        }
    }

    internal System.Windows.Media.ImageSource? GenerateQrCodeBitmap(string url)
    {
        if (_qrCodeGenerator is null || string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        bool[,] matrix = _qrCodeGenerator.GenerateMatrix(url);
        int size = matrix.GetLength(0);
        int quietZone = 2;
        int total = size + (quietZone * 2);
        int scale = 8;
        int pixelDim = total * scale;
        byte[] pixels = new byte[pixelDim * pixelDim * 4];
        Array.Fill<byte>(pixels, 255);

        for (int r = 0; r < size; r++)
        {
            for (int c = 0; c < size; c++)
            {
                if (matrix[r, c])
                {
                    int startY = (r + quietZone) * scale;
                    int startX = (c + quietZone) * scale;
                    for (int y = 0; y < scale; y++)
                    {
                        for (int x = 0; x < scale; x++)
                        {
                            int offset = (((startY + y) * pixelDim) + startX + x) * 4;
                            pixels[offset] = 0;     // B
                            pixels[offset + 1] = 0; // G
                            pixels[offset + 2] = 0; // R
                            pixels[offset + 3] = 255; // A
                        }
                    }
                }
            }
        }

        System.Windows.Media.Imaging.BitmapSource bitmap = System.Windows.Media.Imaging.BitmapSource.Create(
            pixelDim,
            pixelDim,
            96,
            96,
            System.Windows.Media.PixelFormats.Bgra32,
            null,
            pixels,
            pixelDim * 4);
        bitmap.Freeze();
        return bitmap;
    }

    [RelayCommand]
    public void CloseQrCode()
    {
        IsQrCodeVisible = false;
    }

    [RelayCommand]
    public void OpenSettings()
    {
        OpenSettingsRequested?.Invoke(this, EventArgs.Empty);
    }

    public void ReloadSettings()
    {
        string? oldPlaylistUrl = CollaborativePlaylistUrl;
        CollaborativePlaylistUrl = _settingsService.CurrentSettings.CollaborativePlaylistUrl;
        IsWebDropActive = _settingsService.CurrentSettings.EnableWebDropServer;

        if (!string.IsNullOrWhiteSpace(CollaborativePlaylistUrl) &&
            !string.Equals(oldPlaylistUrl, CollaborativePlaylistUrl, StringComparison.OrdinalIgnoreCase))
        {
            if (!_settingsService.CurrentSettings.HasJoinedCollaborativePlaylist)
            {
                IsFirstJoinPromptVisible = true;
            }

            _ = ImportPlaylistFromYouTubeAsync(CollaborativePlaylistUrl);
        }
        else if (string.IsNullOrWhiteSpace(CollaborativePlaylistUrl))
        {
            IsFirstJoinPromptVisible = false;
        }

        if (_telephonyManager.ActiveProvider is { } activeProvider)
        {
            TelephonyProviderName = activeProvider.DisplayName;
        }

        if (_audioDeviceEnumerator is not null && !string.IsNullOrEmpty(_settingsService.CurrentSettings.SelectedAudioDeviceId))
        {
            AudioDeviceOption? device = _audioDeviceEnumerator.GetAvailableRenderDevices()
                .FirstOrDefault(d => d.Id == _settingsService.CurrentSettings.SelectedAudioDeviceId);
            if (device is not null)
            {
                ActiveAudioDeviceName = device.DisplayName;
            }
        }

        ConfigurePlaylistAutoSyncLoop();
    }

    [RelayCommand]
    public void ToggleMute()
    {
        if (IsMuted)
        {
            IsMuted = false;
            MasterVolume = _lastUnmutedVolume > 0 ? _lastUnmutedVolume : 0.8;
        }
        else
        {
            _lastUnmutedVolume = MasterVolume > 0 ? MasterVolume : 0.8;
            IsMuted = true;
            MasterVolume = 0.0;
        }
    }

    [RelayCommand]
    public void ClearQueue()
    {
        _group.Playlist.Clear();
        SyncQueuedItems();
        AutoSavePlaylist();
        StatusMessage = "Warteschlange geleert";
    }

    [RelayCommand]
    public void OpenWebDropInBrowser()
    {
        if (!string.IsNullOrWhiteSpace(WebDropUrl))
        {
            try
            {
                ProcessStarter(new ProcessStartInfo
                {
                    FileName = WebDropUrl,
                    UseShellExecute = true
                });
            }
            catch
            {
                StatusMessage = "Konnte Browser nicht öffnen";
            }
        }
    }

    [RelayCommand]
    public void ToggleQueueExpanded()
    {
        IsQueueExpanded = !IsQueueExpanded;
    }

    [RelayCommand]
    public void CopyWebDropUrl()
    {
        if (!string.IsNullOrWhiteSpace(WebDropUrl))
        {
            try
            {
                ClipboardAction(WebDropUrl);
                StatusMessage = "WebDrop-Link kopiert!";
            }
            catch
            {
                StatusMessage = "Kopieren fehlgeschlagen";
            }
        }
    }

    [RelayCommand]
    public void SubmitQuickAdd()
    {
        if (string.IsNullOrWhiteSpace(QuickAddUrl))
        {
            return;
        }

        string url = QuickAddUrl.Trim();
        QuickAddUrl = string.Empty;
        EnqueueRawUrl(url);
    }

    [RelayCommand]
    public void EnqueueRawUrl(string? rawUrl)
    {
        EnqueueRawUrl(rawUrl, null);
    }

    public void EnqueueRawUrl(string? rawUrl, string? submitter = null)
    {
        if (string.IsNullOrWhiteSpace(rawUrl) || !_urlParser.TryExtractVideoId(rawUrl, out string videoId, out Uri normalizedUri))
        {
            StatusMessage = "Invalid YouTube URL";
            return;
        }

        string effectiveSubmitter = !string.IsNullOrWhiteSpace(submitter)
            ? submitter.Trim()
            : _settingsService.CurrentSettings.ClientDisplayName;

        Track track = new(videoId, normalizedUri, videoId, effectiveSubmitter, DateTimeOffset.UtcNow);
        _group.Playlist.Enqueue(track);
        SyncQueuedItems();
        StatusMessage = $"Enqueued: {videoId}";
        AutoSavePlaylist();

        TriggerAddFeedback($"'{videoId}' zur Warteschlange hinzugefügt");

        _ = FetchQueueTrackMetadataAsync(track);

        _ = _lanGroupService.SendQueueSyncAsync(_group.Id, "Enqueue", track);

        if (_playlistWriter is not null && !string.IsNullOrWhiteSpace(CollaborativePlaylistUrl))
        {
            _ = _playlistWriter.AddTrackToPlaylistAsync(CollaborativePlaylistUrl, videoId);
        }

        if (_group.Playlist.CurrentTrack == track)
        {
            MetadataLoadTask = LoadTrackMetadataAsync(track);
        }
    }

    [RelayCommand]
    public void MoveTrackUp(Track? track)
    {
        if (track is null)
        {
            return;
        }

        if (_group.Playlist.MoveUp(track.Id))
        {
            SyncQueuedItems();
            StatusMessage = $"'{track.Title}' nach oben verschoben";
            AutoSavePlaylist();
            _ = _lanGroupService.SendQueueSyncAsync(_group.Id, "Reorder", track);
        }
    }

    [RelayCommand]
    public void MoveTrackDown(Track? track)
    {
        if (track is null)
        {
            return;
        }

        if (_group.Playlist.MoveDown(track.Id))
        {
            SyncQueuedItems();
            StatusMessage = $"'{track.Title}' nach unten verschoben";
            AutoSavePlaylist();
            _ = _lanGroupService.SendQueueSyncAsync(_group.Id, "Reorder", track);
        }
    }

    [RelayCommand]
    public void RemoveTrack(Track? track)
    {
        if (track is null)
        {
            return;
        }

        if (_group.Playlist.Remove(track.Id))
        {
            SyncQueuedItems();
            StatusMessage = $"Removed: {track.Title}";
            AutoSavePlaylist();
            _ = _lanGroupService.SendQueueSyncAsync(_group.Id, "Remove", track);
        }
    }


    internal void SyncQueuedItems()
    {
        RunOnUi(() =>
        {
            QueuedItems.Clear();
            foreach (Track item in _group.Playlist.QueuedTracks)
            {
                QueuedItems.Add(item);
            }

            QueuedCount = _group.Playlist.Count;
            SyncCurrentTrackDisplayIfEmpty();
        });
    }

    private void SyncCurrentTrackDisplayIfEmpty()
    {
        if (_group.Playlist.CurrentTrack is { } current &&
            (string.IsNullOrWhiteSpace(CurrentTrackTitle) ||
             CurrentTrackTitle == "Kein Song aktiv" ||
             CurrentTrackTitle == "No song playing"))
        {
            CurrentTrackTitle = current.Title;
            CurrentTrackArtist = current.SubmittedBy;
            CurrentTrackThumbnailUrl = current.ThumbnailUrl;
        }
    }

    private async Task LoadTrackMetadataAsync(Track track)
    {
        TrackMetadata? meta = await _metadataFetcher.FetchMetadataAsync(track.VideoId).ConfigureAwait(true);
        if (meta is not null)
        {
            CurrentTrackTitle = meta.Title;
            CurrentTrackArtist = meta.AuthorName;
            CurrentTrackThumbnailUrl = meta.ThumbnailUri?.ToString();
        }
        else
        {
            CurrentTrackTitle = track.Title;
            CurrentTrackArtist = track.SubmittedBy;
            CurrentTrackThumbnailUrl = null;
        }

        _systrayService?.UpdateTooltip($"burroQueue: {CurrentTrackTitle}");
        _systrayService?.ShowNotification(
            "burroQueue – Jetzt läuft",
            $"{CurrentTrackTitle} ({CurrentTrackArtist})",
            CurrentTrackThumbnailUrl ?? track.ThumbnailUrl);

        _skipVotePool = new SkipVotePool(track.Id, requiredVotes: 3);
        UpdateSkipVoteStatus();
    }


    [RelayCommand]
    public async Task VoteToSkipAsync()
    {
        if (_group.Playlist.CurrentTrack is null)
        {
            return;
        }

        if (_skipVotePool.TrackId != _group.Playlist.CurrentTrack.Id)
        {
            _skipVotePool = new SkipVotePool(_group.Playlist.CurrentTrack.Id, requiredVotes: 3);
        }

        bool voted = _skipVotePool.CastVote(_localClientId);
        UpdateSkipVoteStatus();

        _ = _lanGroupService.SendSkipVoteAsync(_group.Id, _group.Playlist.CurrentTrack.VideoId, _localClientId);

        if (_skipVotePool.IsThresholdReached)
        {
            StatusMessage = "Vote threshold reached! Skipping song...";
            await SkipAsync().ConfigureAwait(true);
        }
    }

    private void UpdateSkipVoteStatus()
    {
        SkipVoteStatus = $"{_skipVotePool.CurrentVotes} / {_skipVotePool.RequiredVotes} Votes";
    }

    [RelayCommand]
    public async Task RenameGroupAsync(string? newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
        {
            return;
        }

        _group.Rename(newName);
        GroupName = _group.Name;

        await _lanGroupService.SendGroupActionAsync(_group.Id, _localClientId, "Rename", _group.Name).ConfigureAwait(false);
    }

    [RelayCommand]
    public async Task PlayAsync()
    {
        if (_group.Playlist.CurrentTrack is null && _group.Playlist.Count > 0)
        {
            _group.Playlist.AdvanceNext();
            SyncQueuedItems();
        }

        _group.Play();
        PlaybackState = _group.PlaybackState;
        RunOnUi(() =>
        {
            SyncCurrentTrackDisplayIfEmpty();
            _playbackTimer?.Start();
        });

        if (_group.Playlist.CurrentTrack is { } current)
        {
            if (_audioPlayerBackend.CurrentState == PlaybackState.Paused &&
                _audioPlayerBackend.ActiveTrack?.VideoId == current.VideoId)
            {
                await _audioPlayerBackend.ResumeAsync().ConfigureAwait(false);
            }
            else
            {
                StatusMessage = $"Playing: {current.Title}";
                await _audioPlayerBackend.PlayTrackAsync(current).ConfigureAwait(false);
            }
        }

        await _lanGroupService.SendPlaybackCommandAsync(_group.Id, _localClientId, "Play").ConfigureAwait(false);
    }

    [RelayCommand]
    public async Task PlayTrackNowAsync(Track? track)
    {
        if (track is null)
        {
            return;
        }

        int index = -1;
        for (int i = 0; i < _group.Playlist.QueuedTracks.Count; i++)
        {
            if (_group.Playlist.QueuedTracks[i].Id == track.Id)
            {
                index = i;
                break;
            }
        }

        if (index > 0)
        {
            _group.Playlist.Move(index, 0);
        }

        if (index >= 0)
        {
            await SkipAsync().ConfigureAwait(false);
        }
    }

    [RelayCommand]
    public async Task PauseAsync()
    {
        _group.Pause();
        PlaybackState = _group.PlaybackState;
        RunOnUi(() => _playbackTimer?.Stop());
        await _audioPlayerBackend.PauseAsync().ConfigureAwait(false);
        await _lanGroupService.SendPlaybackCommandAsync(_group.Id, _localClientId, "Pause").ConfigureAwait(false);
    }

    [RelayCommand]
    public async Task StopAsync()
    {
        _group.Stop();
        PlaybackState = _group.PlaybackState;
        RunOnUi(() =>
        {
            _playbackTimer?.Stop();
            PlaybackProgress = 0.0;
            PlaybackTimeFormatted = "00:00 / 00:00";
        });
        await _audioPlayerBackend.StopAsync().ConfigureAwait(false);
        await _lanGroupService.SendPlaybackCommandAsync(_group.Id, _localClientId, "Stop").ConfigureAwait(false);
    }

    [RelayCommand]
    public async Task SkipAsync()
    {
        Track? next = _group.Skip();
        PlaybackState = _group.PlaybackState;
        SyncQueuedItems();
        AutoSavePlaylist();

        if (next is not null)
        {
            StatusMessage = $"Playing: {next.Title}";
            RunOnUi(() =>
            {
                PlaybackProgress = 0.0;
                _playbackTimer?.Start();
            });
            await LoadTrackMetadataAsync(next).ConfigureAwait(true);
            await _audioPlayerBackend.PlayTrackAsync(next).ConfigureAwait(false);
        }
        else
        {
            StatusMessage = "Queue empty";
            CurrentTrackTitle = "No song playing";
            CurrentTrackArtist = string.Empty;
            CurrentTrackThumbnailUrl = null;
            RunOnUi(() =>
            {
                _playbackTimer?.Stop();
                PlaybackProgress = 0.0;
                PlaybackTimeFormatted = "00:00 / 00:00";
            });
            _systrayService?.UpdateTooltip("burroQueue – Keine Wiedergabe");
            await _audioPlayerBackend.StopAsync().ConfigureAwait(false);
        }


        await _lanGroupService.SendPlaybackCommandAsync(_group.Id, _localClientId, "Skip").ConfigureAwait(false);
    }

    [RelayCommand]
    public void OpenCollaborativePlaylist()
    {
        if (string.IsNullOrWhiteSpace(CollaborativePlaylistUrl))
        {
            return;
        }

        try
        {
            ProcessStarter(new ProcessStartInfo
            {
                FileName = CollaborativePlaylistUrl,
                UseShellExecute = true
            });
        }
        catch
        {
            StatusMessage = "Could not open browser for playlist.";
        }
    }

    private void OnTelephonyStateChanged(object? sender, TelephonyCallEventArgs e)
    {
        if (e.IsCallActive)
        {
            _duckingReleaseCts?.Cancel();
            _duckingReleaseCts?.Dispose();
            _duckingReleaseCts = null;

            RunOnUi(() =>
            {
                IsCallActive = true;
                IsAudioDucked = true;
                TelephonyStatusText = $"{e.ProviderId} Anruf aktiv – Lautstärke abgesenkt";
                TelephonyStateSummary = "Anruf aktiv (Ducking aktiv)";

                AudioVolume volume = new(MasterVolume > 0 ? MasterVolume : 0.8, 0.15);
                double target = volume.CalculateEffectiveLevel(e.State);
                _audioController.SetTargetVolume(target, true);
            });
            return;
        }

        int releaseDelaySeconds = Math.Clamp(_settingsService.CurrentSettings?.DuckingReleaseDelaySeconds ?? 0, 0, 120);
        if (releaseDelaySeconds <= 0 || !IsAudioDucked)
        {
            ApplyDuckingRelease();
            return;
        }

        _duckingReleaseCts?.Cancel();
        _duckingReleaseCts?.Dispose();
        _duckingReleaseCts = new CancellationTokenSource();
        CancellationToken token = _duckingReleaseCts.Token;

        RunOnUi(() =>
        {
            TelephonyStatusText = $"Anruf beendet – Lautstärke in {releaseDelaySeconds}s wiederhergestellt";
            TelephonyStateSummary = $"Nachlauf ({releaseDelaySeconds}s)";
        });

        _ = Task.Run(async () =>
        {
            try
            {
                await DelayProvider(TimeSpan.FromSeconds(releaseDelaySeconds), token).ConfigureAwait(false);
                if (!token.IsCancellationRequested)
                {
                    ApplyDuckingRelease();
                }
            }
            catch (OperationCanceledException)
            {
            }
        }, token);
    }

    private void ApplyDuckingRelease()
    {
        RunOnUi(() =>
        {
            IsCallActive = false;
            IsAudioDucked = false;
            TelephonyStatusText = string.Empty;
            TelephonyStateSummary = "Bereit (Überwachung aktiv)";

            double restoreVolume = IsMuted
                ? 0.0
                : (_settingsService.CurrentSettings?.MasterVolume > 0
                    ? _settingsService.CurrentSettings.MasterVolume
                    : (MasterVolume > 0 ? MasterVolume : _lastUnmutedVolume));

            AudioVolume volume = new(restoreVolume, 0.15);
            double target = volume.CalculateEffectiveLevel(TelephonyState.Idle);
            _audioController.SetTargetVolume(target, false);
        });
    }

    private void OnGroupActionReceived(object? sender, GroupActionMessage action)
    {
        if (action.GroupId != _group.Id.Value || action.ActorClientId == _localClientId.Value)
        {
            return;
        }

        if (action.ActionType == "Rename" && !string.IsNullOrWhiteSpace(action.Payload))
        {
            _group.Rename(action.Payload);
            GroupName = _group.Name;
        }
        else if (action.ActionType == "Join")
        {
            string peerName = !string.IsNullOrWhiteSpace(action.Payload) ? action.Payload : "Ein neues Mitglied";
            RunOnUi(() =>
            {
                StatusMessage = $"{peerName} ist der Gruppe beigetreten";
            });
            _systrayService?.ShowNotification("burroQueue – Neues Gruppenmitglied", $"{peerName} ist der Gruppe beigetreten.");
        }
    }

    private void OnPlaybackCommandReceived(object? sender, PlaybackCommandMessage cmd)
    {
        if (cmd.GroupId != _group.Id.Value || cmd.IssuerClientId == _localClientId.Value)
        {
            return;
        }

        switch (cmd.Command)
        {
            case "Play":
                _group.Play();
                PlaybackState = _group.PlaybackState;
                break;
            case "Pause":
                _group.Pause();
                PlaybackState = _group.PlaybackState;
                break;
            case "Stop":
                _group.Stop();
                PlaybackState = _group.PlaybackState;
                break;
            case "Skip":
                PlaybackCommandTask = SkipAsync();
                break;
        }
    }

    private void OnCollaborativePlaylistReceived(object? sender, SharedPlaylistSyncMessage message)
    {
        if (message.GroupId != _group.Id.Value)
        {
            return;
        }

        CollaborativePlaylistUrl = message.CollaborativePlaylistUrl;

        if (!_settingsService.CurrentSettings.HasJoinedCollaborativePlaylist && !string.IsNullOrWhiteSpace(message.CollaborativePlaylistUrl))
        {
            RunOnUi(() => IsFirstJoinPromptVisible = true);
        }

        if (_settingsService.CurrentSettings.AutoOpenCollaborativePlaylistOnJoin)
        {
            OpenCollaborativePlaylist();
        }
    }

    private async void OnTrackPlaybackFinished(object? sender, Track finishedTrack)
    {
        await SkipAsync().ConfigureAwait(false);
    }

    private void OnQueueSyncReceived(object? sender, QueueSyncMessage message)
    {
        if (message.GroupId != _group.Id.Value)
        {
            return;
        }

        RunOnUi(() =>
        {
            if (message.Action == "Enqueue")
            {
                if (_group.Playlist.QueuedTracks.Any(t => t.VideoId == message.VideoId) ||
                    _group.Playlist.CurrentTrack?.VideoId == message.VideoId)
                {
                    return;
                }

                if (Uri.TryCreate(message.TrackUrl, UriKind.Absolute, out Uri? trackUri))
                {
                    Track peerTrack = new(
                        message.VideoId,
                        trackUri,
                        message.Title,
                        message.AddedBy,
                        message.Timestamp);

                    _group.Playlist.Enqueue(peerTrack);
                    SyncQueuedItems();
                    StatusMessage = $"{message.AddedBy} hat hinzugefügt: {message.Title}";
                    AutoSavePlaylist();

                    if (_group.Playlist.CurrentTrack == peerTrack)
                    {
                        MetadataLoadTask = LoadTrackMetadataAsync(peerTrack);
                    }
                }
            }
            else if (message.Action == "Remove")
            {
                Track? existing = _group.Playlist.QueuedTracks.FirstOrDefault(t => t.VideoId == message.VideoId);
                if (existing is not null && _group.Playlist.Remove(existing.Id))
                {
                    SyncQueuedItems();
                    StatusMessage = $"Entfernt von Peer: {message.Title}";
                    AutoSavePlaylist();
                }
            }
        });
    }

    private async void OnSkipVoteReceived(object? sender, SkipVoteMessage vote)
    {
        if (vote.GroupId != _group.Id.Value || vote.VoterClientId == _localClientId.Value)
        {
            return;
        }

        if (_group.Playlist.CurrentTrack is null || _group.Playlist.CurrentTrack.VideoId != vote.TrackId)
        {
            return;
        }

        if (_skipVotePool.TrackId != _group.Playlist.CurrentTrack.Id)
        {
            _skipVotePool = new SkipVotePool(_group.Playlist.CurrentTrack.Id, requiredVotes: 3);
        }

        ClientId remoteVoter = new(vote.VoterClientId);
        bool voted = _skipVotePool.CastVote(remoteVoter);
        if (voted)
        {
            RunOnUi(() =>
            {
                UpdateSkipVoteStatus();
                StatusMessage = $"Skip-Vote eingegangen ({_skipVotePool.CurrentVotes}/{_skipVotePool.RequiredVotes})";
            });

            if (_skipVotePool.IsThresholdReached)
            {
                RunOnUi(() => StatusMessage = "Vote threshold reached! Skipping song...");
                await SkipAsync().ConfigureAwait(true);
            }
        }
    }

    [RelayCommand]
    public void OpenYouTubeInviteAndDismissPrompt()
    {
        IsFirstJoinPromptVisible = false;
        _settingsService.CurrentSettings.HasJoinedCollaborativePlaylist = true;
        AutoSaveSettings();
        OpenCollaborativePlaylist();
    }

    [RelayCommand]
    public void DismissFirstJoinPrompt()
    {
        IsFirstJoinPromptVisible = false;
        _settingsService.CurrentSettings.HasJoinedCollaborativePlaylist = true;
        AutoSaveSettings();
    }

    public void TriggerAddFeedback(string message)
    {
        RunOnUi(() =>
        {
            LastAddedFeedbackText = message;
            IsAddFeedbackVisible = true;
        });

        _feedbackCts?.Cancel();
        _feedbackCts = new CancellationTokenSource();
        CancellationToken token = _feedbackCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(3500, token).ConfigureAwait(false);
                RunOnUi(() =>
                {
                    if (!token.IsCancellationRequested)
                    {
                        IsAddFeedbackVisible = false;
                    }
                });
            }
            catch (OperationCanceledException)
            {
            }
        }, token);
    }

    private async Task FetchQueueTrackMetadataAsync(Track track)
    {
        try
        {
            TrackMetadata? meta = await _metadataFetcher.FetchMetadataAsync(track.VideoId).ConfigureAwait(false);
            if (meta is not null && !string.IsNullOrWhiteSpace(meta.Title))
            {
                track.SetTitle(meta.Title);
                RunOnUi(() =>
                {
                    int index = QueuedItems.IndexOf(track);
                    if (index >= 0)
                    {
                        QueuedItems.RemoveAt(index);
                        QueuedItems.Insert(index, track);
                    }
                    else
                    {
                        SyncQueuedItems();
                    }
                });
                AutoSavePlaylist();
            }
        }
        catch
        {
            // Fail-safe
        }
    }

    private void OnPlaybackTimerTick(object? sender, EventArgs e)
    {
        TimeSpan pos = _audioPlayerBackend.Position;
        TimeSpan dur = _audioPlayerBackend.Duration;
        if (dur > TimeSpan.Zero)
        {
            PlaybackProgress = Math.Clamp((pos.TotalSeconds / dur.TotalSeconds) * 100.0, 0.0, 100.0);
            PlaybackTimeFormatted = $"{pos:mm\\:ss} / {dur:mm\\:ss}";
        }
        else
        {
            PlaybackProgress = 0.0;
            PlaybackTimeFormatted = $"{pos:mm\\:ss} / --:--";
        }
    }

    [RelayCommand]
    public async Task SyncPlaylistNowAsync()
    {
        if (!string.IsNullOrWhiteSpace(CollaborativePlaylistUrl))
        {
            await ImportPlaylistFromYouTubeAsync(CollaborativePlaylistUrl).ConfigureAwait(false);
        }
    }

    private void ConfigurePlaylistAutoSyncLoop()
    {
        _playlistAutoSyncCts?.Cancel();
        _playlistAutoSyncCts?.Dispose();
        _playlistAutoSyncCts = null;

        AppSettingsData? settings = _settingsService.CurrentSettings;
        if (_playlistReader is null ||
            settings is null ||
            !settings.AutoSyncCollaborativePlaylist ||
            string.IsNullOrWhiteSpace(CollaborativePlaylistUrl))
        {
            return;
        }

        int intervalSeconds = Math.Clamp(settings.AutoSyncPlaylistIntervalSeconds, 15, 3600);
        _playlistAutoSyncCts = new CancellationTokenSource();
        CancellationToken token = _playlistAutoSyncCts.Token;

        _ = Task.Run(async () =>
        {
            using PeriodicTimer timer = new(TimeSpan.FromSeconds(intervalSeconds));
            try
            {
                while (await timer.WaitForNextTickAsync(token).ConfigureAwait(false))
                {
                    string? url = CollaborativePlaylistUrl;
                    if (!string.IsNullOrWhiteSpace(url))
                    {
                        await ImportPlaylistFromYouTubeAsync(url).ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
        }, token);
    }

    public async Task ImportPlaylistFromYouTubeAsync(string? playlistUrl)
    {
        if (_playlistReader is null || string.IsNullOrWhiteSpace(playlistUrl))
        {
            return;
        }

        try
        {
            IReadOnlyList<Track> tracks = await _playlistReader.ReadPlaylistTracksAsync(playlistUrl).ConfigureAwait(false);
            if (tracks.Count > 0)
            {
                RunOnUi(() =>
                {
                    int addedCount = 0;
                    foreach (Track track in tracks)
                    {
                        if (!_group.Playlist.QueuedTracks.Any(t => t.VideoId == track.VideoId) &&
                            _group.Playlist.CurrentTrack?.VideoId != track.VideoId)
                        {
                            _group.Playlist.Enqueue(track);
                            addedCount++;
                        }
                    }

                    if (addedCount > 0)
                    {
                        SyncQueuedItems();
                        AutoSavePlaylist();
                        TriggerAddFeedback($"{addedCount} Songs aus Playlist importiert");
                    }
                });
            }
        }
        catch
        {
            // Fail-safe
        }
    }

    private void OnGroupRenamed()
    {
        RunOnUi(() =>
        {
            GroupName = _group.Name;
            SyncQueuedItems();
        });
    }

    private static void RunOnUi(Action action)
    {
        UiDispatcherRunner(action);
    }

    public void Dispose()
    {
        _playbackTimer?.Stop();
        _group.Renamed -= OnGroupRenamed;
        _feedbackCts?.Cancel();
        _feedbackCts?.Dispose();
        _feedbackCts = null;
        _duckingReleaseCts?.Cancel();
        _duckingReleaseCts?.Dispose();
        _duckingReleaseCts = null;
        _playlistAutoSyncCts?.Cancel();
        _playlistAutoSyncCts?.Dispose();
        _playlistAutoSyncCts = null;
    }
}
