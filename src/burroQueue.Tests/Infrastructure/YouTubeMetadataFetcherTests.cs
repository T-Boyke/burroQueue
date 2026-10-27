using System.Net;
using System.Net.Http;
using burroQueue.Domain.Model;
using burroQueue.Infrastructure.YouTube;
using FluentAssertions;
using Xunit;

namespace burroQueue.Tests.Infrastructure;

public sealed class YouTubeMetadataFetcherTests
{
    [Fact]
    public async Task FetchMetadataAsync_WhenValidOEmbedResponse_ShouldReturnPopulatedMetadata()
    {
        string json = """
        {
            "title": "Never Gonna Give You Up",
            "author_name": "Rick Astley",
            "thumbnail_url": "https://i.ytimg.com/vi/dQw4w9WgXcQ/hqdefault.jpg"
        }
        """;

        using HttpClient client = new(MockHttpMessageHandler.WithJsonResponse(json));
        YouTubeMetadataFetcher fetcher = new(client);

        TrackMetadata meta = await fetcher.FetchMetadataAsync("dQw4w9WgXcQ");

        meta.Title.Should().Be("Never Gonna Give You Up");
        meta.AuthorName.Should().Be("Rick Astley");
        meta.ThumbnailUri.Should().Be(new Uri("https://i.ytimg.com/vi/dQw4w9WgXcQ/hqdefault.jpg"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task FetchMetadataAsync_WhenVideoIdEmpty_ShouldReturnUnknownMetadata(string? emptyId)
    {
        using HttpClient client = new(MockHttpMessageHandler.WithStatusCode(HttpStatusCode.OK));
        YouTubeMetadataFetcher fetcher = new(client);

        TrackMetadata meta = await fetcher.FetchMetadataAsync(emptyId!);

        meta.Title.Should().Be("Unknown Track");
        meta.AuthorName.Should().Be("Unknown Artist");
        meta.ThumbnailUri.Should().BeNull();
    }

    [Fact]
    public async Task FetchMetadataAsync_WhenHttpError_ShouldFallbackToUnknown()
    {
        using HttpClient client = new(MockHttpMessageHandler.WithStatusCode(HttpStatusCode.NotFound));
        YouTubeMetadataFetcher fetcher = new(client);

        TrackMetadata meta = await fetcher.FetchMetadataAsync("invalid123");

        meta.Title.Should().Be("invalid123");
        meta.AuthorName.Should().Be("Unknown Artist");
    }

    [Fact]
    public async Task FetchMetadataAsync_WhenNetworkThrowsException_ShouldCatchAndReturnFallback()
    {
        using HttpClient client = new(MockHttpMessageHandler.WithException(new HttpRequestException("DNS failed")));
        YouTubeMetadataFetcher fetcher = new(client);

        TrackMetadata meta = await fetcher.FetchMetadataAsync("vid999");

        meta.Title.Should().Be("vid999");
        meta.AuthorName.Should().Be("Unknown Artist");
    }

    [Fact]
    public void Constructor_WithoutArguments_ShouldNotThrow()
    {
        YouTubeMetadataFetcher fetcher = new();
        fetcher.Should().NotBeNull();
    }

    [Fact]
    public async Task FetchMetadataAsync_WhenJsonPropertiesMissing_ShouldFallbackToDefaults()
    {
        string emptyJson = "{}";
        using HttpClient client = new(MockHttpMessageHandler.WithJsonResponse(emptyJson));
        YouTubeMetadataFetcher fetcher = new(client);

        TrackMetadata meta = await fetcher.FetchMetadataAsync("customVid");

        meta.Title.Should().Be("customVid");
        meta.AuthorName.Should().Be("YouTube Artist");
        meta.ThumbnailUri.Should().BeNull();
    }

    [Fact]
    public async Task FetchMetadataAsync_WhenInvalidJsonReturned_ShouldCatchAndReturnFallback()
    {
        using HttpClient client = new(MockHttpMessageHandler.WithJsonResponse("not valid json at all"));
        YouTubeMetadataFetcher fetcher = new(client);

        TrackMetadata meta = await fetcher.FetchMetadataAsync("corruptVid");

        meta.Title.Should().Be("corruptVid");
        meta.AuthorName.Should().Be("Unknown Artist");
    }
}

