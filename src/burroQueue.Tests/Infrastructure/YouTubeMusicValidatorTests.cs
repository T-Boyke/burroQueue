using System.Net;
using burroQueue.Domain.Model;
using burroQueue.Infrastructure.YouTube;
using FluentAssertions;
using Xunit;

namespace burroQueue.Tests.Infrastructure;

public sealed class YouTubeMusicValidatorTests
{
    [Fact]
    public async Task ValidateAsync_WhenAllFourSignalsConfirmVerifiedMusic_ShouldReturnTrue()
    {
        const string innerTubeJson = """
        {
          "videoDetails": {
            "videoId": "5NV6Rdv1a3I",
            "title": "Get Lucky (Official Audio)",
            "author": "Daft Punk - Topic",
            "lengthSeconds": "248",
            "musicVideoType": "MUSIC_VIDEO_TYPE_ATV"
          },
          "microformat": {
            "playerMicroformatRenderer": {
              "category": "Music"
            }
          }
        }
        """;

        YouTubeMusicValidator validator = new(_ => Task.FromResult<string?>(innerTubeJson));

        MusicValidationResult result = await validator.ValidateAsync("5NV6Rdv1a3I", TimeSpan.FromMinutes(12));

        result.IsVerifiedMusic.Should().BeTrue();
        result.VideoId.Should().Be("5NV6Rdv1a3I");
        result.Title.Should().Be("Get Lucky (Official Audio)");
        result.Author.Should().Be("Daft Punk - Topic");
        result.Duration.Should().Be(TimeSpan.FromSeconds(248));
        result.RejectionReason.Should().BeNull();
    }

    [Fact]
    public async Task ValidateAsync_WhenPodcastOrTutorialExceedsMaxDurationOrNonMusicCategory_ShouldRejectWithReason()
    {
        const string nonMusicJson = """
        {
          "videoDetails": {
            "videoId": "tut12345678",
            "title": "C# 15 Complete 3 Hour Course",
            "author": "Code Academy",
            "lengthSeconds": "10800"
          },
          "microformat": {
            "playerMicroformatRenderer": {
              "category": "Education"
            }
          }
        }
        """;

        YouTubeMusicValidator validator = new(_ => Task.FromResult<string?>(nonMusicJson));

        MusicValidationResult result = await validator.ValidateAsync("tut12345678", TimeSpan.FromMinutes(12));

        result.IsVerifiedMusic.Should().BeFalse();
        result.RejectionReason.Should().NotBeNullOrWhiteSpace();
    }
}
