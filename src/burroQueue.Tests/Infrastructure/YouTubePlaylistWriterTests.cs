using System.Net;
using System.Net.Http;
using burroQueue.Infrastructure.YouTube;
using FluentAssertions;
using Xunit;

namespace burroQueue.Tests.Infrastructure;

public sealed class YouTubePlaylistWriterTests
{
    [Theory]
    [InlineData(null, "dQw4w9WgXcQ")]
    [InlineData("", "dQw4w9WgXcQ")]
    [InlineData("   ", "dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/playlist?list=PL123&jct=tok", null)]
    [InlineData("https://www.youtube.com/playlist?list=PL123&jct=tok", "")]
    [InlineData("https://www.youtube.com/playlist?list=PL123&jct=tok", "   ")]
    public async Task AddTrackToPlaylistAsync_WhenArgumentsInvalid_ShouldReturnFalse(string? playlistUrl, string? videoId)
    {
        using HttpClient client = new(MockHttpMessageHandler.WithStatusCode(HttpStatusCode.OK));
        YouTubePlaylistWriter writer = new(client, ytDlpPath: string.Empty);

        bool result = await writer.AddTrackToPlaylistAsync(playlistUrl!, videoId!);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task AddTrackToPlaylistAsync_WhenInnertubeReturnsSuccess_ShouldReturnTrue()
    {
        string pageHtml = """
        <html>
        <script>ytcfg.set({"INNERTUBE_API_KEY":"AIzaSyMockKey123","INNERTUBE_CONTEXT":{"client":{"clientName":"WEB","clientVersion":"2.20261028.01.00"}}});</script>
        </html>
        """;

        using HttpClient client = new(MockHttpMessageHandler.WithJsonResponse(pageHtml));
        YouTubePlaylistWriter writer = new(client, ytDlpPath: string.Empty);

        bool result = await writer.AddTrackToPlaylistAsync(
            "https://www.youtube.com/playlist?list=PL123456&jct=collabToken99",
            "dQw4w9WgXcQ");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task AddTrackToPlaylistAsync_WhenHttpFails_ShouldReturnFalseGracefully()
    {
        using HttpClient client = new(MockHttpMessageHandler.WithException(new HttpRequestException("Offline")));
        YouTubePlaylistWriter writer = new(client, ytDlpPath: string.Empty);

        bool result = await writer.AddTrackToPlaylistAsync(
            "https://www.youtube.com/playlist?list=PL123456&jct=collabToken99",
            "dQw4w9WgXcQ");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task AddTrackToPlaylistAsync_WhenYtDlpRunnerSucceeds_ShouldReturnTrue()
    {
        Func<string, string, CancellationToken, Task<bool>> original = YouTubePlaylistWriter.ProcessRunner;
        try
        {
            YouTubePlaylistWriter.ProcessRunner = (_, _, _) => Task.FromResult(true);
            using HttpClient client = new(MockHttpMessageHandler.WithStatusCode(HttpStatusCode.InternalServerError));
            YouTubePlaylistWriter writer = new(client, ytDlpPath: "C:\\mock\\yt-dlp.exe");

            bool result = await writer.AddTrackToPlaylistAsync(
                "https://www.youtube.com/playlist?list=PL123456&jct=collabToken99",
                "dQw4w9WgXcQ");

            result.Should().BeTrue();
        }
        finally
        {
            YouTubePlaylistWriter.ProcessRunner = original;
        }
    }

    [Fact]
    public void ExtractPlaylistIdAndJct_ShouldParseParametersCorrectly()
    {
        (string playlistId, string? jct) = YouTubePlaylistWriter.ExtractPlaylistIdAndJct(
            "https://www.youtube.com/playlist?list=PLabc123&jct=tokenXYZ");

        playlistId.Should().Be("PLabc123");
        jct.Should().Be("tokenXYZ");
    }
}

