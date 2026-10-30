using burroQueue.Domain.Model;
using burroQueue.Infrastructure.YouTube;
using FluentAssertions;
using Xunit;

namespace burroQueue.Tests.Infrastructure;

public sealed class InnerTubePlaylistParserTests
{
    [Fact]
    public void ParseBrowseResponse_WhenWebCollaborativePlaylistWithContributorsAndOwnerFallback_ShouldExtractCollaboratorNamesAvatarsArtistsAndContinuation()
    {
        string json = """
        {
          "header": {
            "playlistHeaderRenderer": {
              "title": { "simpleText": "Büro Jukebox" },
              "ownerText": { "runs": [ { "text": "Clara (Host)" } ] },
              "ownerAvatar": {
                "thumbnails": [
                  { "url": "https://yt3.ggpht.com/ytc/clara_owner_avatar=s88-c-k", "width": 88, "height": 88 }
                ]
              }
            }
          },
          "contents": {
            "twoColumnBrowseResultsRenderer": {
              "tabs": [
                {
                  "tabRenderer": {
                    "content": {
                      "sectionListRenderer": {
                        "contents": [
                          {
                            "itemSectionRenderer": {
                              "contents": [
                                {
                                  "playlistVideoListRenderer": {
                                    "contents": [
                                      {
                                        "playlistVideoRenderer": {
                                          "videoId": "eVTXPUF4Oz4",
                                          "title": { "runs": [ { "text": "In The End" } ] },
                                          "shortBylineText": { "runs": [ { "text": "Linkin Park" } ] },
                                          "contributors": [
                                            {
                                              "contributorName": { "runs": [ { "text": "Added by Tobias" } ] },
                                              "contributorAvatar": {
                                                "thumbnails": [
                                                  { "url": "//yt3.ggpht.com/ytc/tobias_avatar=s48-c-k", "width": 48, "height": 48 },
                                                  { "url": "https://yt3.ggpht.com/ytc/tobias_avatar=s88-c-k", "width": 88, "height": 88 }
                                                ]
                                              }
                                            }
                                          ]
                                        }
                                      },
                                      {
                                        "playlistVideoRenderer": {
                                          "videoId": "5NV6Rdv1a3I",
                                          "title": { "runs": [ { "text": "Get Lucky" } ] },
                                          "shortBylineText": { "runs": [ { "text": "Daft Punk" } ] }
                                        }
                                      },
                                      {
                                        "continuationItemRenderer": {
                                          "continuationEndpoint": {
                                            "continuationCommand": {
                                              "token": "CONT_TOKEN_PAGE_2"
                                            }
                                          }
                                        }
                                      }
                                    ]
                                  }
                                }
                              ]
                            }
                          }
                        ]
                      }
                    }
                  }
                }
              ]
            }
          }
        }
        """;

        InnerTubePlaylistParseResult parsed = InnerTubePlaylistParser.ParseBrowseResponse(json);

        parsed.ContinuationToken.Should().Be("CONT_TOKEN_PAGE_2");
        parsed.Tracks.Should().HaveCount(2);

        Track first = parsed.Tracks[0];
        first.VideoId.Should().Be("eVTXPUF4Oz4");
        first.Title.Should().Be("In The End");
        first.ArtistName.Should().Be("Linkin Park");
        first.SubmittedBy.Should().Be("Tobias");
        first.SubmitterAvatarUrl.Should().Be(new Uri("https://yt3.ggpht.com/ytc/tobias_avatar=s88-c-k"));
        first.IsSyncedFromPlaylist.Should().BeTrue();

        Track second = parsed.Tracks[1];
        second.VideoId.Should().Be("5NV6Rdv1a3I");
        second.Title.Should().Be("Get Lucky");
        second.ArtistName.Should().Be("Daft Punk");
        second.SubmittedBy.Should().Be("Clara (Host)");
        second.SubmitterAvatarUrl.Should().Be(new Uri("https://yt3.ggpht.com/ytc/clara_owner_avatar=s88-c-k"));
        second.IsSyncedFromPlaylist.Should().BeTrue();
    }

    [Fact]
    public void ParseBrowseResponse_WhenYouTubeMusicWebRemixCollaborativePlaylist_ShouldExtractContributedByItemData()
    {
        string json = """
        {
          "contents": {
            "musicPlaylistShelfRenderer": {
              "contents": [
                {
                  "musicResponsiveListItemRenderer": {
                    "flexColumns": [
                      {
                        "musicResponsiveListItemFlexColumnRenderer": {
                          "text": { "runs": [ { "text": "Feel Good Inc." } ] }
                        }
                      },
                      {
                        "musicResponsiveListItemFlexColumnRenderer": {
                          "text": { "runs": [ { "text": "Gorillaz" } ] }
                        }
                      }
                    ],
                    "playNavigationEndpoint": {
                      "watchEndpoint": {
                        "videoId": "HyHNuVaZJ-k"
                      }
                    },
                    "itemColumnData": {
                      "musicCollaborativePlaylistContributedByItemData": {
                        "contributorName": { "runs": [ { "text": "Hinzugefügt von Max" } ] },
                        "contributorAvatar": {
                          "thumbnails": [
                            { "url": "https://yt3.googleusercontent.com/a/max_ytm_avatar=s64-c-k", "width": 64, "height": 64 }
                          ]
                        }
                      }
                    }
                  }
                }
              ]
            }
          }
        }
        """;

        InnerTubePlaylistParseResult parsed = InnerTubePlaylistParser.ParseBrowseResponse(json);

        parsed.Tracks.Should().ContainSingle();
        Track track = parsed.Tracks[0];
        track.VideoId.Should().Be("HyHNuVaZJ-k");
        track.Title.Should().Be("Feel Good Inc.");
        track.ArtistName.Should().Be("Gorillaz");
        track.SubmittedBy.Should().Be("Max");
        track.SubmitterAvatarUrl.Should().Be(new Uri("https://yt3.googleusercontent.com/a/max_ytm_avatar=s64-c-k"));
        track.IsSyncedFromPlaylist.Should().BeTrue();
    }
}
