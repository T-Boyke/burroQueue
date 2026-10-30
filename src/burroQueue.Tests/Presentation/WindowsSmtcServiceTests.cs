using burroQueue.Domain.Model;
using burroQueue.Presentation.Services;
using FluentAssertions;
using Xunit;

namespace burroQueue.Tests.Presentation;

public sealed class WindowsSmtcServiceTests
{
    [Fact]
    public void UpdatePlaybackAndTrackMetadata_ShouldMaintainStateAndRaiseTransportEvents()
    {
        WindowsSmtcService smtc = new();
        bool playPauseRaised = false;
        bool nextRaised = false;
        smtc.PlayPauseRequested += (_, _) => playPauseRaised = true;
        smtc.NextTrackRequested += (_, _) => nextRaised = true;

        smtc.UpdateTrackMetadata("Get Lucky", "Daft Punk", "https://img.youtube.com/vi/5NV6Rdv1a3I/hqdefault.jpg");
        smtc.UpdatePlaybackState(PlaybackState.Playing);

        smtc.CurrentTitle.Should().Be("Get Lucky");
        smtc.CurrentArtist.Should().Be("Daft Punk");
        smtc.CurrentThumbnailUrl.Should().Be("https://img.youtube.com/vi/5NV6Rdv1a3I/hqdefault.jpg");
        smtc.CurrentPlaybackState.Should().Be(PlaybackState.Playing);

        smtc.SimulateButtonPressed("PlayPause");
        smtc.SimulateButtonPressed("Next");

        playPauseRaised.Should().BeTrue();
        nextRaised.Should().BeTrue();
    }
}

