using System.Collections.ObjectModel;
using System.Diagnostics;
using burroQueue.Domain.Model;
using burroQueue.Infrastructure.Audio;
using burroQueue.Infrastructure.Configuration;
using burroQueue.Infrastructure.Network;
using burroQueue.Infrastructure.Network.Messages;
using burroQueue.Infrastructure.Telephony;
using burroQueue.Infrastructure.Web;
using burroQueue.Infrastructure.YouTube;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace burroQueue.Presentation.ViewModels;

public sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly BurroGroup _group;
    private readonly IYouTubeUrlParser _urlParser;
    private readonly IYouTubeMetadataFetcher _metadataFetcher;
    private readonly ITelephonyManager _telephonyManager;
    private readonly ILanGroupService _lanGroupService;
    private readonly IAudioOutputController _audioController;
    private readonly IAppSettingsService _settingsService;
    private readonly IEmbeddedWebDropServer _webDropServer;
    private readonly ClientId _localClientId = ClientId.New();
    private SkipVotePool _skipVotePool;

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
    private string _webDropUrl = "http://localhost:8080";

    [ObservableProperty]
    private bool _isQueueExpanded;

    public ObservableCollection<Track> QueuedItems { get; } = [];

    public MainWindowViewModel(
        BurroGroup group,
        IYouTubeUrlParser urlParser,
        IYouTubeMetadataFetcher metadataFetcher,
        ITelephonyManager telephonyManager,
        ILanGroupService lanGroupService,
        IAudioOutputController audioController,
        IAppSettingsService settingsService,
        IEmbeddedWebDropServer webDropServer)
    {
        _group = group ?? throw new ArgumentNullException(nameof(group));
        _urlParser = urlParser ?? throw new ArgumentNullException(nameof(urlParser));
        _metadataFetcher = metadataFetcher ?? throw new ArgumentNullException(nameof(metadataFetcher));
        _telephonyManager = telephonyManager ?? throw new ArgumentNullException(nameof(telephonyManager));
        _lanGroupService = lanGroupService ?? throw new ArgumentNullException(nameof(lanGroupService));
        _audioController = audioController ?? throw new ArgumentNullException(nameof(audioController));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _webDropServer = webDropServer ?? throw new ArgumentNullException(nameof(webDropServer));

        _groupName = _group.Name;
        _collaborativePlaylistUrl = _settingsService.CurrentSettings.CollaborativePlaylistUrl;
        _skipVotePool = new SkipVotePool(TrackId.New(), requiredVotes: 3);

        SyncQueuedItems();

        _telephonyManager.StateChanged += OnTelephonyStateChanged;
        _lanGroupService.GroupActionReceived += OnGroupActionReceived;
        _lanGroupService.PlaybackCommandReceived += OnPlaybackCommandReceived;
        _lanGroupService.CollaborativePlaylistReceived += OnCollaborativePlaylistReceived;
        _webDropServer.LinkDropped += (_, droppedUrl) => EnqueueRawUrl(droppedUrl);
    }

    [RelayCommand]
    public void ToggleQueueExpanded()
    {
        IsQueueExpanded = !IsQueueExpanded;
    }

    [RelayCommand]
    public void EnqueueRawUrl(string? rawUrl)
    {
        if (string.IsNullOrWhiteSpace(rawUrl) || !_urlParser.TryExtractVideoId(rawUrl, out string videoId, out Uri normalizedUri))
        {
            StatusMessage = "Invalid YouTube URL";
            return;
        }

        Track track = new(videoId, normalizedUri, videoId, _settingsService.CurrentSettings.ClientDisplayName, DateTimeOffset.UtcNow);
        _group.Playlist.Enqueue(track);
        SyncQueuedItems();
        StatusMessage = $"Enqueued: {videoId}";

        if (_group.Playlist.CurrentTrack == track)
        {
            _ = LoadTrackMetadataAsync(track);
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
        }
    }

    private void SyncQueuedItems()
    {
        QueuedItems.Clear();
        foreach (Track item in _group.Playlist.QueuedTracks)
        {
            QueuedItems.Add(item);
        }

        QueuedCount = _group.Playlist.Count;
    }

    private async Task LoadTrackMetadataAsync(Track track)
    {
        TrackMetadata meta = await _metadataFetcher.FetchMetadataAsync(track.VideoId).ConfigureAwait(true);
        CurrentTrackTitle = meta.Title;
        CurrentTrackArtist = meta.AuthorName;
        CurrentTrackThumbnailUrl = meta.ThumbnailUri?.ToString();
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

        bool voted = _skipVotePool.CastVote(_localClientId);
        UpdateSkipVoteStatus();

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
        _group.Play();
        PlaybackState = _group.PlaybackState;
        await _lanGroupService.SendPlaybackCommandAsync(_group.Id, _localClientId, "Play").ConfigureAwait(false);
    }

    [RelayCommand]
    public async Task PauseAsync()
    {
        _group.Pause();
        PlaybackState = _group.PlaybackState;
        await _lanGroupService.SendPlaybackCommandAsync(_group.Id, _localClientId, "Pause").ConfigureAwait(false);
    }

    [RelayCommand]
    public async Task StopAsync()
    {
        _group.Stop();
        PlaybackState = _group.PlaybackState;
        await _lanGroupService.SendPlaybackCommandAsync(_group.Id, _localClientId, "Stop").ConfigureAwait(false);
    }

    [RelayCommand]
    public async Task SkipAsync()
    {
        Track? next = _group.Skip();
        PlaybackState = _group.PlaybackState;
        SyncQueuedItems();

        if (next is not null)
        {
            StatusMessage = $"Playing: {next.Title}";
            await LoadTrackMetadataAsync(next).ConfigureAwait(true);
        }
        else
        {
            StatusMessage = "Queue empty";
            CurrentTrackTitle = "No song playing";
            CurrentTrackArtist = string.Empty;
            CurrentTrackThumbnailUrl = null;
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
            Process.Start(new ProcessStartInfo
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
        IsCallActive = e.IsCallActive;
        TelephonyStatusText = e.IsCallActive
            ? $"{e.ProviderId} Anruf aktiv – Lautstärke abgesenkt"
            : string.Empty;

        AudioVolume volume = new(0.8, 0.15);
        double target = volume.CalculateEffectiveLevel(e.State);
        _audioController.SetTargetVolume(target, e.IsCallActive);
    }

    private void OnGroupActionReceived(object? sender, GroupActionMessage action)
    {
        if (action.GroupId != _group.Id.Value)
        {
            return;
        }

        if (action.ActionType == "Rename" && !string.IsNullOrWhiteSpace(action.Payload))
        {
            _group.Rename(action.Payload);
            GroupName = _group.Name;
        }
    }

    private void OnPlaybackCommandReceived(object? sender, PlaybackCommandMessage cmd)
    {
        if (cmd.GroupId != _group.Id.Value)
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
                _ = SkipAsync();
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

        if (_settingsService.CurrentSettings.AutoOpenCollaborativePlaylistOnJoin)
        {
            OpenCollaborativePlaylist();
        }
    }
}
