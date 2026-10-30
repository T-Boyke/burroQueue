using burroQueue.Domain.Model;
using burroQueue.Infrastructure.Audio;
using burroQueue.Infrastructure.Playback;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace burroQueue.Tests.Infrastructure;

public sealed class AudioPlayerBackendTests : IAsyncLifetime, IDisposable
{
    private readonly IAudioOutputController _audioOutput;
    private readonly IStreamUriResolver _streamResolver;
    private readonly AudioPlayerBackend _backend;

    public AudioPlayerBackendTests()
    {
        _audioOutput = Substitute.For<IAudioOutputController>();
        _audioOutput.CurrentVolume.Returns(0.8);
        _streamResolver = Substitute.For<IStreamUriResolver>();
        _backend = new AudioPlayerBackend(_audioOutput, _streamResolver);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await _backend.DisposeAsync();
    }

    public void Dispose()
    {
        _backend.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    [Fact]
    public async Task PlayTrackAsync_ShouldResolveStreamAndTransitionToPlaying()
    {
        Track track = new("abc12345", new Uri("https://www.youtube.com/watch?v=abc12345"), "Sample Track", "Alice", DateTimeOffset.UtcNow);
        Uri streamUri = new("https://stream.provider/audio.mp4");
        _streamResolver.ResolveAudioStreamUriAsync("abc12345", Arg.Any<CancellationToken>()).Returns(streamUri);

        await _backend.PlayTrackAsync(track);

        _backend.CurrentState.Should().Be(PlaybackState.Playing);
        _backend.ActiveTrack.Should().Be(track);
        await _streamResolver.Received(1).ResolveAudioStreamUriAsync("abc12345", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PauseAndResume_ShouldTransitionStateProperly()
    {
        Track track = new("abc12345", new Uri("https://www.youtube.com/watch?v=abc12345"), "Sample Track", "Alice", DateTimeOffset.UtcNow);
        _streamResolver.ResolveAudioStreamUriAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new Uri("https://stream.provider/audio.mp4"));

        await _backend.PlayTrackAsync(track);
        await _backend.PauseAsync();
        _backend.CurrentState.Should().Be(PlaybackState.Paused);

        await _backend.ResumeAsync();
        _backend.CurrentState.Should().Be(PlaybackState.Playing);
    }

    [Fact]
    public async Task StopAsync_ShouldResetStateAndActiveTrack()
    {
        Track track = new("abc12345", new Uri("https://www.youtube.com/watch?v=abc12345"), "Sample Track", "Alice", DateTimeOffset.UtcNow);
        _streamResolver.ResolveAudioStreamUriAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new Uri("https://stream.provider/audio.mp4"));

        await _backend.PlayTrackAsync(track);
        await _backend.StopAsync();

        _backend.CurrentState.Should().Be(PlaybackState.Stopped);
        _backend.ActiveTrack.Should().BeNull();
    }

    [Fact]
    public async Task NotifyTrackFinished_ShouldFireTrackPlaybackFinishedEvent()
    {
        Track track = new("abc12345", new Uri("https://www.youtube.com/watch?v=abc12345"), "Sample Track", "Alice", DateTimeOffset.UtcNow);
        _streamResolver.ResolveAudioStreamUriAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new Uri("https://stream.provider/audio.mp4"));

        Track? finishedTrack = null;
        _backend.TrackPlaybackFinished += (_, t) => finishedTrack = t;

        await _backend.PlayTrackAsync(track);
        _backend.NotifyTrackFinished();

        finishedTrack.Should().Be(track);
        _backend.CurrentState.Should().Be(PlaybackState.Stopped);
        _backend.ActiveTrack.Should().BeNull();
    }

    [Fact]
    public void VolumeChangedOnOutputController_ShouldUpdateBackendVolume()
    {
        _backend.CurrentVolume.Should().Be(0.8);

        _audioOutput.VolumeChanged += Raise.Event<EventHandler<double>>(this, 0.25);

        _backend.CurrentVolume.Should().Be(0.25);
    }

    [Fact]
    public async Task PlayTrackAsync_WithMediaStreamPlayer_ShouldOpenAndPlayStream()
    {
        IMediaStreamPlayer player = Substitute.For<IMediaStreamPlayer>();
        await using AudioPlayerBackend backendWithPlayer = new(_audioOutput, _streamResolver, player);

        Track track = new("media-1", new Uri("https://www.youtube.com/watch?v=media-1"), "Media Track", "Bob", DateTimeOffset.UtcNow);
        Uri streamUri = new("https://direct.audio/stream.webm");
        _streamResolver.ResolveAudioStreamUriAsync("media-1", Arg.Any<CancellationToken>()).Returns(streamUri);

        await backendWithPlayer.PlayTrackAsync(track);

        player.Received(1).Open(streamUri);
        player.Received(1).Play();
        player.Volume.Should().Be(_audioOutput.CurrentVolume);

        await backendWithPlayer.PauseAsync();
        player.Received(1).Pause();

        await backendWithPlayer.ResumeAsync();
        player.Received(2).Play();

        _audioOutput.VolumeChanged += Raise.Event<EventHandler<double>>(_audioOutput, 0.45);
        player.Volume.Should().Be(0.45);

        await backendWithPlayer.StopAsync();
        player.Received(1).StopPlayback();
    }

    [Fact]
    public async Task MediaStreamPlayer_WhenMediaEndedFires_ShouldNotifyTrackFinishedAndAdvance()
    {
        IMediaStreamPlayer player = Substitute.For<IMediaStreamPlayer>();
        await using AudioPlayerBackend backendWithPlayer = new(_audioOutput, _streamResolver, player);

        Track track = new("ended-1", new Uri("https://www.youtube.com/watch?v=ended-1"), "Ended Song", "Clara", DateTimeOffset.UtcNow);
        Uri streamUri = new("https://direct.audio/ended.m4a");
        _streamResolver.ResolveAudioStreamUriAsync("ended-1", Arg.Any<CancellationToken>()).Returns(streamUri);

        Track? finishedTrack = null;
        backendWithPlayer.TrackPlaybackFinished += (_, t) => finishedTrack = t;

        await backendWithPlayer.PlayTrackAsync(track);

        // Fire MediaEnded on player
        player.MediaEnded += Raise.Event<EventHandler>(player, EventArgs.Empty);

        finishedTrack.Should().Be(track);
        backendWithPlayer.CurrentState.Should().Be(PlaybackState.Stopped);
        backendWithPlayer.ActiveTrack.Should().BeNull();
    }

    [Fact]
    public async Task PlayTrackAsync_WhenLoudnessNormalizationEnabled_ShouldScalePlayerVolumeUsingResolvedTrackLoudness()
    {
        IMediaStreamPlayer player = Substitute.For<IMediaStreamPlayer>();
        await using AudioPlayerBackend backendWithPlayer = new(_audioOutput, _streamResolver, player);

        Track track = new("loud-1", new Uri("https://www.youtube.com/watch?v=loud-1"), "Loud Song", "Alice", DateTimeOffset.UtcNow);
        Uri streamUri = new("https://direct.audio/loud.m4a");
        _streamResolver.ResolveAudioStreamUriAsync("loud-1", Arg.Any<CancellationToken>()).Returns(streamUri);
        _streamResolver.ResolveTrackLoudnessLufsAsync("loud-1", Arg.Any<CancellationToken>()).Returns(-8.0);

        backendWithPlayer.ConfigureLoudnessNormalization(enabled: true, targetLufs: -14.0);
        await backendWithPlayer.PlayTrackAsync(track);

        // -8.0 LUFS vs -14.0 LUFS target => -6 dB => linear gain ~0.501187 * 0.8 master volume
        player.Volume.Should().BeApproximately(0.8 * 0.501187, 0.002);

        // Toggling normalization off in settings should restore raw master volume immediately
        backendWithPlayer.ConfigureLoudnessNormalization(enabled: false, targetLufs: -14.0);
        player.Volume.Should().Be(0.8);
    }

    [Fact]
    public async Task PrefetchTrackAsync_ShouldResolveStreamAndLoudnessWithoutChangingActiveTrack()
    {
        Track upcoming = new("next-1", new Uri("https://www.youtube.com/watch?v=next-1"), "Upcoming Song", "Bob", DateTimeOffset.UtcNow);
        _streamResolver.ResolveAudioStreamUriAsync("next-1", Arg.Any<CancellationToken>())
            .Returns(new Uri("https://direct.audio/next.m4a"));
        _streamResolver.ResolveTrackLoudnessLufsAsync("next-1", Arg.Any<CancellationToken>())
            .Returns(-12.5);

        await _backend.PrefetchTrackAsync(upcoming);

        _backend.ActiveTrack.Should().BeNull();
        _backend.CurrentState.Should().Be(PlaybackState.Stopped);
        await _streamResolver.Received(1).ResolveAudioStreamUriAsync("next-1", Arg.Any<CancellationToken>());
        await _streamResolver.Received(1).ResolveTrackLoudnessLufsAsync("next-1", Arg.Any<CancellationToken>());
    }
}

