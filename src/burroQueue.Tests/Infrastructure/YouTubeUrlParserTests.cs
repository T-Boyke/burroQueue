using burroQueue.Infrastructure.YouTube;
using FluentAssertions;
using Xunit;

namespace burroQueue.Tests.Infrastructure;

public sealed class YouTubeUrlParserTests
{
    private readonly YouTubeUrlParser _sut = new();

    [Theory]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ", "dQw4w9WgXcQ")]
    [InlineData("https://youtu.be/dQw4w9WgXcQ?si=12345", "dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/shorts/dQw4w9WgXcQ", "dQw4w9WgXcQ")]
    [InlineData("https://music.youtube.com/watch?v=dQw4w9WgXcQ", "dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/embed/dQw4w9WgXcQ", "dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/live/dQw4w9WgXcQ?feature=share", "dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/v/dQw4w9WgXcQ", "dQw4w9WgXcQ")]
    [InlineData("dQw4w9WgXcQ", "dQw4w9WgXcQ")]
    public void TryExtractVideoId_WhenValidYouTubeUrl_ShouldReturnTrueAndNormalizedUri(string rawUrl, string expectedId)
    {
        bool success = _sut.TryExtractVideoId(rawUrl, out string videoId, out Uri normalizedUri);

        success.Should().BeTrue();
        videoId.Should().Be(expectedId);
        normalizedUri.Should().Be(new Uri($"https://www.youtube.com/watch?v={expectedId}"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("https://example.com/watch?v=123")]
    [InlineData("not-a-url")]
    public void TryExtractVideoId_WhenInvalidInput_ShouldReturnFalse(string invalidInput)
    {
        bool success = _sut.TryExtractVideoId(invalidInput, out string videoId, out _);

        success.Should().BeFalse();
        videoId.Should().BeEmpty();
    }
}

