using burroQueue.Infrastructure.Playback;
using FluentAssertions;
using Xunit;

namespace burroQueue.Tests.Infrastructure;

public sealed class YouTubeAudioStreamResolverTests
{
    [Fact]
    public async Task ResolveAudioStreamUriAsync_WhenValidVideoId_ShouldReturnStreamUri()
    {
        YouTubeAudioStreamResolver resolver = new();

        Uri? uri = await resolver.ResolveAudioStreamUriAsync("dQw4w9WgXcQ");

        uri.Should().NotBeNull();
        uri!.ToString().Should().Be("https://www.youtube.com/watch?v=dQw4w9WgXcQ");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task ResolveAudioStreamUriAsync_WhenEmptyVideoId_ShouldReturnNull(string? emptyId)
    {
        YouTubeAudioStreamResolver resolver = new();

        Uri? uri = await resolver.ResolveAudioStreamUriAsync(emptyId!);

        uri.Should().BeNull();
    }
}
