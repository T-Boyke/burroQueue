using System.Collections.ObjectModel;
using System.Diagnostics;
using burroQueue.Domain.Model;
using burroQueue.Domain.Repositories;
using burroQueue.Domain.Services;
using burroQueue.Infrastructure.Audio;
using burroQueue.Infrastructure.Configuration;
using burroQueue.Infrastructure.Cryptography;
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
    private readonly ILocalNetworkInfoService _networkInfoService;
    private readonly IAudioPlayerBackend _audioPlayerBackend;
    private readonly IPlaylistStorageService _playlistStorage;
    private readonly IAudioDeviceEnumerator? _audioDeviceEnumerator;
    private readonly ISystrayNotificationService? _systrayService;
    private readonly IQrCodeSvgGenerator? _qrCodeGenerator;
    private readonly IYouTubePlaylistReader? _playlistReader;
    private readonly IYouTubePlaylistWriter? _playlistWriter;
    private readonly IStreamUriResolver? _streamUriResolver;
    private readonly IEntropySource _entropySource;
    private readonly QueueShuffleEngine _shuffleEngine;
    private readonly IFavoriteQueueRepository? _favoriteRepository;
    private readonly IYouTubeMusicValidator? _musicValidator;
    private readonly ISystemMediaControlsService? _smtcService;
    private readonly Dictionary<string, FavoriteTrackEntry> _favoritesByVideoId = new(StringComparer.Ordinal);
    private string _lastHandledClipboardVideoId = string.Empty;
    private readonly ClientId _localClientId = ClientId.New();
    private readonly HashSet<Guid> _activeRemoteCallPeers = [];
    private bool _localCallActive;
    private SkipVotePool _skipVotePool;
    private bool _isUpdatingVolumeInternally;
    private double _lastUnmutedVolume = 0.8;
    private CancellationTokenSource? _feedbackCts;
    private CancellationTokenSource? _duckingReleaseCts;
    private CancellationTokenSource? _playlistAutoSyncCts;
    private readonly System.Windows.Threading.DispatcherTimer? _playbackTimer;
    private int _checkpointTickCounter;

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
    [NotifyPropertyChangedFor(nameof(IsPlaying))]
    private PlaybackState _playbackState = PlaybackState.Stopped;

    partial void OnPlaybackStateChanged(PlaybackState value)
    {
        _smtcService?.UpdatePlaybackState(value);
    }

    public bool IsPlaying => PlaybackState == PlaybackState.Playing;

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
    private string _currentTrackSubmittedBy = string.Empty;

    [ObservableProperty]
    private Uri? _currentTrackSubmitterAvatarUrl;

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
    private bool _isQueueExpanded = true;

    [ObservableProperty]
    private bool _isQuickAddInputVisible;

    [ObservableProperty]
    private bool _isShuffleEnabled;

    [ObservableProperty]
    private bool _isRepeatAllEnabled;

    [ObservableProperty]
    private bool _hasFavoriteTracks;

    [ObservableProperty]
    private bool _isFavoritesExpanded = true;

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

    public ObservableCollection<QueueTrackItemViewModel> QueueViewItems { get; } = [];

    public ObservableCollection<FavoriteTrackEntry> FavoriteTracks { get; } = [];

    internal Task AutoSaveTask { get; private set; } = Task.CompletedTask;

    internal Task AutoSaveSettingsTask { get; private set; } = Task.CompletedTask;

    internal Task MetadataLoadTask { get; private set; } = Task.CompletedTask;

    internal Task PlaybackCommandTask { get; private set; } = Task.CompletedTask;

    internal Task DuckingReleaseTask { get; private set; } = Task.CompletedTask;

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
        IYouTubePlaylistWriter? playlistWriter = null,
        IStreamUriResolver? streamUriResolver = null,
        IEntropySource? entropySource = null,
        QueueShuffleEngine? shuffleEngine = null,
        IFavoriteQueueRepository? favoriteRepository = null,
        IYouTubeMusicValidator? musicValidator = null,
        ISystemMediaControlsService? smtcService = null)
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
        _streamUriResolver = streamUriResolver;
        _entropySource = entropySource ?? new HardwareCsprngEntropySource();
        _shuffleEngine = shuffleEngine ?? new QueueShuffleEngine();
        _favoriteRepository = favoriteRepository;
        _musicValidator = musicValidator;
        _smtcService = smtcService;
        _networkInfoService = networkInfoService ?? throw new ArgumentNullException(nameof(networkInfoService));

        _groupName = _group.Name;
        _group.Renamed += OnGroupRenamed;
        _collaborativePlaylistUrl = _settingsService.CurrentSettings.CollaborativePlaylistUrl;
        _webDropUrl = _networkInfoService.GetWebDropUrl(_webDropServer.Port > 0 ? _webDropServer.Port : 8080);
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
            _currentTrackArtist = initialTrack.ArtistName ?? initialTrack.SubmittedBy;
            _currentTrackSubmittedBy = initialTrack.SubmittedBy;
            _currentTrackSubmitterAvatarUrl = initialTrack.SubmitterAvatarUrl;
            _currentTrackThumbnailUrl = initialTrack.ThumbnailUrl;
        }

        _telephonyManager.StateChanged += OnTelephonyStateChanged;
        _lanGroupService.GroupActionReceived += OnGroupActionReceived;
        _lanGroupService.PlaybackCommandReceived += OnPlaybackCommandReceived;
        _lanGroupService.CollaborativePlaylistReceived += OnCollaborativePlaylistReceived;
        _lanGroupService.QueueSyncReceived += OnQueueSyncReceived;
        _lanGroupService.SkipVoteReceived += OnSkipVoteReceived;
        _lanGroupService.TelephonyStateReceived += OnPeerTelephonyStateReceived;
        _lanGroupService.QueueOrderSyncReceived += OnQueueOrderSyncReceived;
        _lanGroupService.TrackUpvoteReceived += OnTrackUpvoteReceived;
        _lanGroupService.SetBeaconResponder(() => new DiscoveryBeaconMessage(
            _group.Id.Value,
            _group.Name,
            _localClientId.Value,
            _settingsService.CurrentSettings?.ClientDisplayName ?? Environment.UserName,
            Math.Max(1, _group.Members.Count),
            !string.IsNullOrWhiteSpace(CollaborativePlaylistUrl),
            CollaborativePlaylistUrl,
            DateTimeOffset.UtcNow));
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

        if (_smtcService is not null)
        {
            _smtcService.PlayPauseRequested += async (_, _) => await TogglePlayPauseAsync().ConfigureAwait(false);
            _smtcService.NextTrackRequested += async (_, _) => await SkipAsync().ConfigureAwait(false);
        }
    }

    public async Task InitializeAsync()
    {
        try
        {
            await _playlistStorage.LoadPlaylistAsync(_group.Playlist).ConfigureAwait(false);
            RunOnUi(SyncQueuedItems);

            QueuePlaybackCursor restoredCursor = _playlistStorage.LastLoadedCursor;
            if (_group.Playlist.CurrentTrack is { } restoredTrack && restoredCursor.PlaybackPosition > TimeSpan.Zero)
            {
                await _audioPlayerBackend.PreparePausedAtPositionAsync(restoredTrack, restoredCursor.PlaybackPosition).ConfigureAwait(false);
                RunOnUi(() =>
                {
                    _group.Pause();
                    PlaybackState = _group.PlaybackState;
                    PlaybackTimeFormatted = $"{restoredCursor.PlaybackPosition:mm\\:ss} / --:--";
                    StatusMessage = $"Bereit ab {restoredCursor.PlaybackPosition:mm\\:ss}: {restoredTrack.Title}";
                });
            }
        }
        catch
        {
            // Fail-safe: Fallback auf leere Playlist
        }

        if (_group.Playlist.TotalCount == 0 && !string.IsNullOrWhiteSpace(CollaborativePlaylistUrl))
        {
            await ImportPlaylistFromYouTubeAsync(CollaborativePlaylistUrl).ConfigureAwait(false);
        }

        if (_favoriteRepository is not null)
        {
            try
            {
                IReadOnlyList<FavoriteTrackEntry> loadedFavorites = await _favoriteRepository.LoadFavoritesAsync(_group.Name).ConfigureAwait(false);
                RunOnUi(() =>
                {
                    _favoritesByVideoId.Clear();
                    foreach (FavoriteTrackEntry fav in loadedFavorites)
                    {
                        _favoritesByVideoId[fav.VideoId] = fav;
                    }

                    RefreshFavoriteTracksCollection();
                    SyncQueuedItems();
                });
            }
            catch
            {
            }
        }

        ConfigurePlaylistAutoSyncLoop();
    }

    public QueuePlaybackCursor CapturePlaybackCursor() =>
        new(
            _group.Playlist.CurrentTrack?.VideoId ?? string.Empty,
            _group.Playlist.CurrentTrackIndex,
            _audioPlayerBackend.Position,
            PlaybackState == PlaybackState.Playing);

    private void AutoSavePlaylist()
    {
        QueuePlaybackCursor cursor = CapturePlaybackCursor();
        AutoSaveTask = Task.Run(async () =>
        {
            try
            {
                _playlistStorage.UpdatePlaybackCursor(cursor);
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
        IsWebDropActive = _settingsService.CurrentSettings.EnableWebDropServer || _webDropServer.IsRunning;
        NetworkStatusText = IsWebDropActive ? "LAN-Sync & WebDrop aktiv" : "LAN-Sync aktiv (WebDrop aus)";

        if (IsWebDropActive && !_webDropServer.IsRunning)
        {
            _ = Task.Run(async () =>
            {
                await _webDropServer.StartAsync().ConfigureAwait(false);
                RunOnUi(() => RefreshWebDropEndpointUrl());
            });
        }
        else if (!IsWebDropActive && _webDropServer.IsRunning)
        {
            _ = _webDropServer.StopAsync();
        }
        else
        {
            RefreshWebDropEndpointUrl();
        }

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

    private void RefreshWebDropEndpointUrl()
    {
        int activePort = _webDropServer.Port > 0 ? _webDropServer.Port : 8080;
        string updatedUrl = _networkInfoService.GetWebDropUrl(activePort);
        if (!string.Equals(WebDropUrl, updatedUrl, StringComparison.OrdinalIgnoreCase))
        {
            WebDropUrl = updatedUrl;
            QrCodeSvg = string.Empty;
            QrCodeImage = null;
        }
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
                if (!_webDropServer.IsRunning)
                {
                    _webDropServer.StartAsync().GetAwaiter().GetResult();
                    IsWebDropActive = _settingsService.CurrentSettings.EnableWebDropServer || _webDropServer.IsRunning;
                    RefreshWebDropEndpointUrl();
                }

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
    public void ShowQuickAddInput()
    {
        IsQuickAddInputVisible = true;
    }

    [RelayCommand]
    public void HideQuickAddInputIfEmpty()
    {
        if (string.IsNullOrWhiteSpace(QuickAddUrl))
        {
            IsQuickAddInputVisible = false;
        }
    }

    [RelayCommand]
    public void CancelQuickAddInput()
    {
        QuickAddUrl = string.Empty;
        IsQuickAddInputVisible = false;
    }

    [RelayCommand]
    public void SubmitQuickAdd()
    {
        if (string.IsNullOrWhiteSpace(QuickAddUrl))
        {
            IsQuickAddInputVisible = false;
            return;
        }

        string url = QuickAddUrl.Trim();
        QuickAddUrl = string.Empty;
        IsQuickAddInputVisible = false;
        EnqueueRawUrl(url);
    }

    [RelayCommand]
    public void EnqueueRawUrl(string? rawUrl)
    {
        EnqueueRawUrl(rawUrl, null);
    }

    public async Task<bool> HandleClipboardTextAsync(string? clipboardText)
    {
        string mode = _settingsService.CurrentSettings?.ClipboardDetectionMode ?? "AskToast";
        if (string.Equals(mode, "Off", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(clipboardText))
        {
            return false;
        }

        if (!_urlParser.TryExtractVideoId(clipboardText, out string videoId, out _))
        {
            return false;
        }

        if (string.Equals(videoId, _lastHandledClipboardVideoId, StringComparison.Ordinal) ||
            _group.Playlist.AllTracks.Any(t => t.VideoId == videoId))
        {
            return false;
        }

        if (string.Equals(mode, "AskToast", StringComparison.OrdinalIgnoreCase))
        {
            _lastHandledClipboardVideoId = videoId;
            RunOnUi(() =>
            {
                QuickAddUrl = clipboardText.Trim();
                IsQuickAddInputVisible = true;
                StatusMessage = $"YouTube-Link erkannt ({videoId}) – Enter drücken zum Einreihen";
            });
            _systrayService?.ShowNotification(
                "burroQueue – YouTube-Link erkannt",
                $"Link ({videoId}) im Eingabefeld bereitgestellt",
                $"https://img.youtube.com/vi/{videoId}/hqdefault.jpg");
            return true;
        }

        if (string.Equals(mode, "AutoQueueVerifiedMusic", StringComparison.OrdinalIgnoreCase))
        {
            int maxMins = Math.Clamp(_settingsService.CurrentSettings?.MaxMusicDurationMinutes ?? 12, 1, 60);
            IYouTubeMusicValidator validator = _musicValidator ?? new YouTubeMusicValidator();
            MusicValidationResult validation = await validator.ValidateAsync(videoId, TimeSpan.FromMinutes(maxMins)).ConfigureAwait(false);
            if (!validation.IsVerifiedMusic)
            {
                RunOnUi(() => StatusMessage = $"Zwischenablage übersprungen (kein Musik-Track: {validation.RejectionReason})");
                return false;
            }

            _lastHandledClipboardVideoId = videoId;
            RunOnUi(() => EnqueueRawUrl(clipboardText));
            return true;
        }

        return false;
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

            QueueViewItems.Clear();
            int currentIdx = _group.Playlist.CurrentTrackIndex;
            string localUser = GetLocalVoterUsername();
            IReadOnlyList<Track> allTracks = _group.Playlist.AllTracks;
            for (int i = 0; i < allTracks.Count; i++)
            {
                Track item = allTracks[i];
                bool isCurrent = i == currentIdx;
                bool hasPlayed = currentIdx >= 0 && i < currentIdx;
                int upvoteCount = 0;
                bool isUpvotedByLocal = false;
                if (_favoritesByVideoId.TryGetValue(item.VideoId, out FavoriteTrackEntry? fav))
                {
                    upvoteCount = fav.Likes;
                    isUpvotedByLocal = fav.IsLikedBy(localUser);
                }

                QueueViewItems.Add(new QueueTrackItemViewModel(
                    item,
                    isCurrentlyPlaying: isCurrent,
                    hasPlayed: hasPlayed,
                    upvoteCount: upvoteCount,
                    isUpvotedByLocalUser: isUpvotedByLocal));
            }

            QueuedCount = _group.Playlist.Count;
            SyncCurrentTrackDisplayIfEmpty();
        });
    }

    private string GetLocalVoterUsername()
    {
        string? configured = _settingsService.CurrentSettings?.ClientDisplayName;
        return string.IsNullOrWhiteSpace(configured) ? Environment.UserName : configured.Trim();
    }

    [RelayCommand]
    public void ToggleFavoritesExpanded()
    {
        IsFavoritesExpanded = !IsFavoritesExpanded;
    }

    [RelayCommand]
    public void EnqueueFavoriteTrack(FavoriteTrackEntry? favorite)
    {
        if (favorite is null)
        {
            return;
        }

        EnqueueRawUrl(favorite.Url.ToString());
    }

    [RelayCommand]
    public async Task ToggleTrackUpvoteAsync(Track? track)
    {
        if (track is null)
        {
            return;
        }

        string voter = GetLocalVoterUsername();
        if (!_favoritesByVideoId.TryGetValue(track.VideoId, out FavoriteTrackEntry? entry))
        {
            entry = new FavoriteTrackEntry(track.VideoId, track.Url, track.Title);
            _favoritesByVideoId[track.VideoId] = entry;
        }
        else
        {
            entry.UpdateTitle(track.Title);
        }

        bool isNowUpvoted = entry.ToggleVote(voter);
        if (entry.Likes <= 0)
        {
            _favoritesByVideoId.Remove(track.VideoId);
        }

        RunOnUi(() =>
        {
            RefreshFavoriteTracksCollection();
            foreach (QueueTrackItemViewModel itemVm in QueueViewItems.Where(q => q.VideoId == track.VideoId))
            {
                itemVm.UpvoteCount = entry.Likes;
                itemVm.IsUpvotedByLocalUser = isNowUpvoted;
            }
        });

        await SaveGroupFavoritesSafeAsync().ConfigureAwait(false);
        await _lanGroupService.SendTrackUpvoteAsync(
            _group.Id,
            track.VideoId,
            track.Url.ToString(),
            track.Title,
            voter,
            isNowUpvoted).ConfigureAwait(false);
    }

    private void RefreshFavoriteTracksCollection()
    {
        FavoriteTracks.Clear();
        foreach (FavoriteTrackEntry fav in _favoritesByVideoId.Values
            .Where(f => f.Likes > 0)
            .OrderByDescending(f => f.Likes)
            .ThenBy(f => f.Title, StringComparer.OrdinalIgnoreCase))
        {
            FavoriteTracks.Add(fav);
        }

        HasFavoriteTracks = FavoriteTracks.Count > 0;
    }

    private async Task SaveGroupFavoritesSafeAsync()
    {
        if (_favoriteRepository is null)
        {
            return;
        }

        try
        {
            await _favoriteRepository.SaveFavoritesAsync(
                _group.Name,
                _favoritesByVideoId.Values.Where(f => f.Likes > 0).ToList()).ConfigureAwait(false);
        }
        catch
        {
        }
    }

    private void SyncCurrentTrackDisplayIfEmpty()
    {
        if (_group.Playlist.CurrentTrack is { } current &&
            (string.IsNullOrWhiteSpace(CurrentTrackTitle) ||
             CurrentTrackTitle == "Kein Song aktiv" ||
             CurrentTrackTitle == "No song playing"))
        {
            CurrentTrackTitle = current.Title;
            CurrentTrackArtist = current.ArtistName ?? current.SubmittedBy;
            CurrentTrackSubmittedBy = current.SubmittedBy;
            CurrentTrackSubmitterAvatarUrl = current.SubmitterAvatarUrl;
            CurrentTrackThumbnailUrl = current.ThumbnailUrl;
        }
    }

    private async Task LoadTrackMetadataAsync(Track track)
    {
        TrackMetadata? meta = await _metadataFetcher.FetchMetadataAsync(track.VideoId).ConfigureAwait(true);
        CurrentTrackSubmittedBy = track.SubmittedBy;
        CurrentTrackSubmitterAvatarUrl = track.SubmitterAvatarUrl;

        if (meta is not null)
        {
            CurrentTrackTitle = meta.Title;
            CurrentTrackArtist = meta.AuthorName;
            CurrentTrackThumbnailUrl = meta.ThumbnailUri?.ToString();
        }
        else
        {
            CurrentTrackTitle = track.Title;
            CurrentTrackArtist = track.ArtistName ?? track.SubmittedBy;
            CurrentTrackThumbnailUrl = null;
        }

        _systrayService?.UpdateTooltip($"burroQueue: {CurrentTrackTitle}");
        string toastThumb = CurrentTrackThumbnailUrl ?? track.ThumbnailUrl;
        _smtcService?.UpdateTrackMetadata(CurrentTrackTitle, CurrentTrackArtist, toastThumb);
        if (track.SubmitterAvatarUrl is not null || track.IsSyncedFromPlaylist)
        {
            _systrayService?.ShowNotification(
                "burroQueue – Jetzt läuft",
                $"{CurrentTrackTitle} ({CurrentTrackArtist})",
                toastThumb,
                $"von {track.SubmittedBy}",
                track.SubmitterAvatarUrl);
        }
        else
        {
            _systrayService?.ShowNotification(
                "burroQueue – Jetzt läuft",
                $"{CurrentTrackTitle} ({CurrentTrackArtist})",
                toastThumb);
        }

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
    public Task TogglePlayPauseAsync() =>
        IsPlaying ? PauseAsync() : PlayAsync();

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

        Track? selected = _group.JumpTo(track.Id);
        if (selected is null)
        {
            return;
        }

        PlaybackState = _group.PlaybackState;
        SyncQueuedItems();
        AutoSavePlaylist();

        StatusMessage = $"Playing: {selected.Title}";
        RunOnUi(() =>
        {
            PlaybackProgress = 0.0;
            _playbackTimer?.Start();
        });

        await LoadTrackMetadataAsync(selected).ConfigureAwait(true);
        await _audioPlayerBackend.PlayTrackAsync(selected).ConfigureAwait(false);
    }

    [RelayCommand]
    public async Task PauseAsync()
    {
        _group.Pause();
        PlaybackState = _group.PlaybackState;
        RunOnUi(() => _playbackTimer?.Stop());
        await _audioPlayerBackend.PauseAsync().ConfigureAwait(false);
        AutoSavePlaylist();
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
    public async Task ToggleShuffleAsync()
    {
        if (IsShuffleEnabled)
        {
            _shuffleEngine.DisableShuffle(_group.Playlist);
            IsShuffleEnabled = false;
            StatusMessage = "Shuffle deaktiviert (Original-Reihenfolge)";
        }
        else
        {
            _shuffleEngine.EnableShuffle(_group.Playlist, _entropySource);
            IsShuffleEnabled = true;
            StatusMessage = "Shuffle aktiviert (Hardware-CSPRNG)";
        }

        SyncQueuedItems();
        AutoSavePlaylist();

        string[] orderedIds = _group.Playlist.AllTracks.Select(t => t.VideoId).ToArray();
        await _lanGroupService.SendQueueOrderSyncAsync(_group.Id, _localClientId, IsShuffleEnabled, orderedIds).ConfigureAwait(false);
    }

    [RelayCommand]
    public void ToggleRepeatAll()
    {
        IsRepeatAllEnabled = !IsRepeatAllEnabled;
        _group.Playlist.IsRepeatAllEnabled = IsRepeatAllEnabled;
        StatusMessage = IsRepeatAllEnabled ? "Wiederholen (Alle) aktiv" : "Wiederholen deaktiviert";
    }

    [RelayCommand]
    public async Task SkipAsync()
    {
        if (_group.Playlist.CurrentTrack is { } finished)
        {
            _shuffleEngine.RecordPlayed(finished.VideoId);
        }

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
            CurrentTrackSubmittedBy = string.Empty;
            CurrentTrackSubmitterAvatarUrl = null;
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
        _localCallActive = e.IsCallActive;
        string displayName = _settingsService.CurrentSettings?.ClientDisplayName ?? Environment.UserName;
        _ = _lanGroupService.SendTelephonyStateAsync(_group.Id, _localClientId, displayName, e.ProviderId, e.IsCallActive);

        if (e.IsCallActive)
        {
            ActivateDucking($"{e.ProviderId} Anruf aktiv – Lautstärke abgesenkt", e.State);
            return;
        }

        if (_activeRemoteCallPeers.Count == 0)
        {
            ScheduleOrApplyDuckingRelease();
        }
    }

    private void OnPeerTelephonyStateReceived(object? sender, PeerTelephonyStateMessage message)
    {
        if (message.GroupId != _group.Id.Value || message.ClientId == _localClientId.Value)
        {
            return;
        }

        if (message.IsCallActive)
        {
            _activeRemoteCallPeers.Add(message.ClientId);
            ActivateDucking(
                $"{message.ClientDisplayName} ({message.ProviderId}) im Gespräch – Lautstärke abgesenkt",
                TelephonyState.ActiveCall);
            return;
        }

        _activeRemoteCallPeers.Remove(message.ClientId);
        if (!_localCallActive && _activeRemoteCallPeers.Count == 0)
        {
            ScheduleOrApplyDuckingRelease();
        }
    }

    private void ActivateDucking(string statusText, TelephonyState state)
    {
        _duckingReleaseCts?.Cancel();
        _duckingReleaseCts?.Dispose();
        _duckingReleaseCts = null;

        RunOnUi(() =>
        {
            IsCallActive = true;
            IsAudioDucked = true;
            TelephonyStatusText = statusText;
            TelephonyStateSummary = "Anruf aktiv (Ducking aktiv)";

            AudioVolume volume = new(MasterVolume > 0 ? MasterVolume : 0.8, 0.15);
            double target = volume.CalculateEffectiveLevel(state);
            _audioController.SetTargetVolume(target, true);
        });
    }

    private void ScheduleOrApplyDuckingRelease()
    {
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

        DuckingReleaseTask = Task.Run(async () =>
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

    private void OnQueueOrderSyncReceived(object? sender, QueueOrderSyncMessage message)
    {
        if (message.GroupId != _group.Id.Value || message.IssuerClientId == _localClientId.Value || message.OrderedVideoIds.Count == 0)
        {
            return;
        }

        RunOnUi(() =>
        {
            Dictionary<string, Queue<Track>> byVideoId = new(StringComparer.Ordinal);
            foreach (Track existing in _group.Playlist.AllTracks)
            {
                if (!byVideoId.TryGetValue(existing.VideoId, out Queue<Track>? bucket))
                {
                    bucket = new Queue<Track>();
                    byVideoId[existing.VideoId] = bucket;
                }

                bucket.Enqueue(existing);
            }

            List<Track> reordered = new(_group.Playlist.AllTracks.Count);
            foreach (string vid in message.OrderedVideoIds)
            {
                if (byVideoId.TryGetValue(vid, out Queue<Track>? bucket) && bucket.Count > 0)
                {
                    reordered.Add(bucket.Dequeue());
                }
            }

            foreach (Queue<Track> remaining in byVideoId.Values)
            {
                while (remaining.Count > 0)
                {
                    reordered.Add(remaining.Dequeue());
                }
            }

            _group.Playlist.ReplaceOrderPreservingCursor(reordered);
            IsShuffleEnabled = message.IsShuffleEnabled;
            SyncQueuedItems();
            AutoSavePlaylist();
        });
    }

    private void OnTrackUpvoteReceived(object? sender, TrackUpvoteMessage message)
    {
        if (message.GroupId != _group.Id.Value || string.IsNullOrWhiteSpace(message.VideoId))
        {
            return;
        }

        RunOnUi(() =>
        {
            if (!_favoritesByVideoId.TryGetValue(message.VideoId, out FavoriteTrackEntry? entry))
            {
                if (!message.IsUpvoted || !Uri.TryCreate(message.TrackUrl, UriKind.Absolute, out Uri? url))
                {
                    return;
                }

                entry = new FavoriteTrackEntry(message.VideoId, url, message.Title);
                _favoritesByVideoId[message.VideoId] = entry;
            }
            else
            {
                entry.UpdateTitle(message.Title);
            }

            entry.SetVote(message.VoterUsername, message.IsUpvoted);
            if (entry.Likes <= 0)
            {
                _favoritesByVideoId.Remove(message.VideoId);
            }

            string localUser = GetLocalVoterUsername();
            RefreshFavoriteTracksCollection();
            foreach (QueueTrackItemViewModel itemVm in QueueViewItems.Where(q => q.VideoId == message.VideoId))
            {
                itemVm.UpvoteCount = entry.Likes;
                itemVm.IsUpvotedByLocalUser = entry.IsLikedBy(localUser);
            }

            _ = SaveGroupFavoritesSafeAsync();
        });
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
                    Uri? peerAvatar = null;
                    if (!string.IsNullOrWhiteSpace(message.AddedByAvatarUrl))
                    {
                        _ = Uri.TryCreate(message.AddedByAvatarUrl, UriKind.Absolute, out peerAvatar);
                    }

                    Track peerTrack = new(
                        message.VideoId,
                        trackUri,
                        message.Title,
                        message.AddedBy,
                        message.Timestamp,
                        artistName: message.ArtistName,
                        submitterAvatarUrl: peerAvatar);

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

                    QueueViewItems.FirstOrDefault(q => q.Id == track.Id)?.RefreshMetadata();
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

        if (++_checkpointTickCounter >= 15)
        {
            _checkpointTickCounter = 0;
            AutoSavePlaylist();
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
                    PlaylistReconciliationResult result = _group.Playlist.ReconcileRemotePlaylist(tracks);
                    foreach (Track removed in result.RemovedTracks)
                    {
                        _streamUriResolver?.RemoveCachedAudioFile(removed.VideoId);
                    }

                    if (result.HasChanges)
                    {
                        SyncQueuedItems();
                        AutoSavePlaylist();
                        if (result.AddedTracks.Count > 0)
                        {
                            TriggerAddFeedback($"{result.AddedTracks.Count} Songs aus Playlist importiert");
                        }
                        else if (result.RemovedTracks.Count > 0)
                        {
                            TriggerAddFeedback($"{result.RemovedTracks.Count} gelöschte Playlist-Songs entfernt");
                        }
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
