using System.Net;
using System.Net.Http;
using burroQueue.Domain.Model;
using burroQueue.Infrastructure.YouTube;
using FluentAssertions;
using Xunit;

namespace burroQueue.Tests.Infrastructure;

public sealed class YouTubePlaylistReaderTests
{
    [Fact]
    public async Task ReadPlaylistTracksAsync_WhenInvidiousReturnsValidVideos_ShouldParseTracks()
    {
        string json = """
        {
            "title": "Office Jukebox",
            "videos": [
                {
                    "title": "Song One",
                    "videoId": "vid-1",
                    "author": "Artist A"
                },
                {
                    "title": "Song Two",
                    "videoId": "vid-2",
                    "author": "Artist B"
                }
            ]
        }
        """;

        using HttpClient client = new(MockHttpMessageHandler.WithJsonResponse(json));
        YouTubePlaylistReader reader = new(client, ytDlpPath: string.Empty);

        IReadOnlyList<Track> tracks = await reader.ReadPlaylistTracksAsync("https://www.youtube.com/playlist?list=PL12345&jct=tok1");

        tracks.Should().HaveCount(2);
        tracks[0].VideoId.Should().Be("vid-1");
        tracks[0].Title.Should().Be("Song One");
        tracks[0].SubmittedBy.Should().Be("Artist A");
        tracks[0].Url.Should().Be(new Uri("https://www.youtube.com/watch?v=vid-1"));

        tracks[1].VideoId.Should().Be("vid-2");
        tracks[1].Title.Should().Be("Song Two");
    }

    [Fact]
    public async Task ReadPlaylistTracksAsync_WhenUrlOrIdIsEmpty_ShouldReturnEmptyList()
    {
        using HttpClient client = new(MockHttpMessageHandler.WithStatusCode(HttpStatusCode.OK));
        YouTubePlaylistReader reader = new(client);

        IReadOnlyList<Track> tracks = await reader.ReadPlaylistTracksAsync("   ");

        tracks.Should().BeEmpty();
    }

    [Fact]
    public async Task ReadPlaylistTracksAsync_WhenInvidiousReturnsError_ShouldReturnEmptyList()
    {
        using HttpClient client = new(MockHttpMessageHandler.WithStatusCode(HttpStatusCode.NotFound));
        YouTubePlaylistReader reader = new(client, ytDlpPath: string.Empty);

        IReadOnlyList<Track> tracks = await reader.ReadPlaylistTracksAsync("PL_non_existent");

        tracks.Should().BeEmpty();
    }

    [Fact]
    public async Task ReadPlaylistTracksAsync_WhenNetworkThrowsException_ShouldHandleGracefully()
    {
        using HttpClient client = new(MockHttpMessageHandler.WithException(new HttpRequestException("Network failure")));
        YouTubePlaylistReader reader = new(client, ytDlpPath: string.Empty);

        IReadOnlyList<Track> tracks = await reader.ReadPlaylistTracksAsync("PL12345");

        tracks.Should().BeEmpty();
    }

    [Fact]
    public async Task ReadPlaylistTracksAsync_WhenYtDlpAvailable_ShouldParseFlatPlaylistJson()
    {
        string ytDlpJson = """
        {
            "id": "PLTest",
            "title": "Test Playlist",
            "_type": "playlist",
            "entries": [
                {
                    "id": "abc12345",
                    "title": "Bohemian Rhapsody",
                    "channel": "Queen",
                    "duration": 354
                },
                {
                    "id": "def67890",
                    "title": "Don't Stop Me Now",
                    "uploader": "Queen Official",
                    "duration": 210
                }
            ]
        }
        """;

        var original = YouTubePlaylistReader.ProcessRunner;
        try
        {
            YouTubePlaylistReader.ProcessRunner = (bin, args, ct) => Task.FromResult<string?>(ytDlpJson);
            YouTubePlaylistReader reader = new(ytDlpPath: "C:\\mock\\yt-dlp.exe");

            IReadOnlyList<Track> tracks = await reader.ReadPlaylistTracksAsync("https://www.youtube.com/playlist?list=PLTest");

            tracks.Should().HaveCount(2);
            tracks[0].VideoId.Should().Be("abc12345");
            tracks[0].Title.Should().Be("Bohemian Rhapsody");
            tracks[0].SubmittedBy.Should().Be("Queen");

            tracks[1].VideoId.Should().Be("def67890");
            tracks[1].Title.Should().Be("Don't Stop Me Now");
            tracks[1].SubmittedBy.Should().Be("Queen Official");
        }
        finally
        {
            YouTubePlaylistReader.ProcessRunner = original;
        }
    }
}

