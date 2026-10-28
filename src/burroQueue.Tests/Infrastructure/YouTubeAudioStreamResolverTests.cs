using System.IO;
using System.Net;
using System.Net.Http;
using burroQueue.Infrastructure.Playback;
using FluentAssertions;
using Xunit;

namespace burroQueue.Tests.Infrastructure;

public sealed class YouTubeAudioStreamResolverTests
{
    [Fact]
    public async Task ResolveAudioStreamUriAsync_WhenYtDlpSucceeds_ShouldReturnDirectAudioUri()
    {
        Func<string, string, CancellationToken, Task<string?>> original = YouTubeAudioStreamResolver.ProcessRunner;
        try
        {
            YouTubeAudioStreamResolver.ProcessRunner = (_, _, _) => Task.FromResult<string?>("https://stream.googlevideo.com/playback.webm");
            YouTubeAudioStreamResolver resolver = new(ytDlpPath: "C:\\dummy\\yt-dlp.exe");

            Uri? uri = await resolver.ResolveAudioStreamUriAsync("video123");

            uri.Should().NotBeNull();
            uri!.ToString().Should().Be("https://stream.googlevideo.com/playback.webm");
        }
        finally
        {
            YouTubeAudioStreamResolver.ProcessRunner = original;
        }
    }

    [Fact]
    public async Task ResolveAudioStreamUriAsync_WhenYtDlpThrows_ShouldFallbackToInvidiousOrWatchUrl()
    {
        Func<string, string, CancellationToken, Task<string?>> original = YouTubeAudioStreamResolver.ProcessRunner;
        try
        {
            YouTubeAudioStreamResolver.ProcessRunner = (_, _, _) => throw new InvalidOperationException("Process crashed");
            YouTubeAudioStreamResolver resolver = new(ytDlpPath: "C:\\dummy\\yt-dlp.exe");

            Uri? uri = await resolver.ResolveAudioStreamUriAsync("video456");

            uri.Should().NotBeNull();
            uri!.ToString().Should().Contain("video456");
        }
        finally
        {
            YouTubeAudioStreamResolver.ProcessRunner = original;
        }
    }

    [Fact]
    public async Task ResolveAudioStreamUriAsync_WhenInvidiousReturnsAudioFormats_ShouldExtractAudioUrl()
    {
        string json = """
        {
            "adaptiveFormats": [
                { "type": "video/mp4", "url": "https://inv.video/stream.mp4" },
                { "type": "audio/webm; codecs=\"opus\"", "url": "https://inv.audio/stream.webm" }
            ]
        }
        """;

        using HttpClient client = new(MockHttpMessageHandler.WithJsonResponse(json));

        // Pass empty ytDlpPath to trigger Invidious path directly
        YouTubeAudioStreamResolver resolver = new(httpClient: client, ytDlpPath: string.Empty);

        Uri? uri = await resolver.ResolveAudioStreamUriAsync("inv123");

        uri.Should().NotBeNull();
        uri!.ToString().Should().Be("https://inv.audio/stream.webm");
    }

    [Fact]
    public async Task ResolveAudioStreamUriAsync_WhenInvidiousThrows_ShouldFallbackToWatchUrl()
    {
        using HttpClient client = new(MockHttpMessageHandler.WithException(new HttpRequestException("API down")));
        YouTubeAudioStreamResolver resolver = new(httpClient: client, ytDlpPath: string.Empty);

        Uri? uri = await resolver.ResolveAudioStreamUriAsync("inv456");

        uri.Should().NotBeNull();
        uri!.ToString().Should().Be("https://www.youtube.com/watch?v=inv456");
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

    [Fact]
    public void IsYtDlpAvailable_WhenYtDlpPathProvided_ShouldBeTrue()
    {
        YouTubeAudioStreamResolver resolver = new(ytDlpPath: "C:\\dummy\\yt-dlp.exe");
        resolver.IsYtDlpAvailable.Should().BeTrue();
    }

    [Fact]
    public void IsYtDlpAvailable_WhenYtDlpPathEmpty_ShouldBeFalse()
    {
        YouTubeAudioStreamResolver resolver = new(ytDlpPath: string.Empty);
        resolver.IsYtDlpAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task ResolveAudioStreamUriAsync_WhenYtDlpDownloadsLocalMp4File_ShouldReturnLocalFileUri()
    {
        Func<string, string, CancellationToken, Task<string?>> original = YouTubeAudioStreamResolver.ProcessRunner;
        string testVideoId = $"test_local_{Guid.NewGuid():N}";
        string cacheDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "burroQueue", "cache");
        string expectedFile = Path.Combine(cacheDir, $"{testVideoId}.mp4");

        try
        {
            YouTubeAudioStreamResolver.ProcessRunner = (_, args, _) =>
            {
                args.Should().Contain("player_client=android");
                args.Should().Contain("18/b");
                Directory.CreateDirectory(cacheDir);
                File.WriteAllBytes(expectedFile, [1, 2, 3, 4]);
                return Task.FromResult<string?>("[download] 100%");
            };

            YouTubeAudioStreamResolver resolver = new(ytDlpPath: "C:\\dummy\\yt-dlp.exe");
            Uri? uri = await resolver.ResolveAudioStreamUriAsync(testVideoId);

            uri.Should().NotBeNull();
            uri!.IsFile.Should().BeTrue();
            uri.LocalPath.Should().Be(expectedFile);
        }
        finally
        {
            YouTubeAudioStreamResolver.ProcessRunner = original;
            if (File.Exists(expectedFile))
            {
                File.Delete(expectedFile);
            }
        }
    }
}

