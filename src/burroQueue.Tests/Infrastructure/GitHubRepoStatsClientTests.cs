using System.Net;
using System.Net.Http;
using burroQueue.Infrastructure.Diagnostics;
using FluentAssertions;
using Xunit;

namespace burroQueue.Tests.Infrastructure;

public sealed class GitHubRepoStatsClientTests
{
    [Fact]
    public async Task FetchSnapshotAsync_WhenApiReturnsUserAndRepoJson_ShouldParseLiveSnapshot()
    {
        MockHttpMessageHandler handler = new(request =>
        {
            string url = request.RequestUri?.ToString() ?? string.Empty;
            if (url.Contains("/users/T-Boyke", StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """{"login":"T-Boyke","name":"Tobias Boyke","avatar_url":"https://avatars.githubusercontent.com/u/37149164?v=4","public_repos":19,"followers":28}""",
                        System.Text.Encoding.UTF8,
                        "application/json")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"stargazers_count":84,"license":{"spdx_id":"AGPL-3.0"}}""",
                    System.Text.Encoding.UTF8,
                    "application/json")
            };
        });

        using HttpClient httpClient = new(handler);
        GitHubRepoStatsClient client = new(httpClient);

        GitHubAboutSnapshot snapshot = await client.FetchSnapshotAsync();

        snapshot.IsLive.Should().BeTrue();
        snapshot.AuthorDisplayName.Should().Be("Tobias Boyke");
        snapshot.AuthorHandle.Should().Be("@T-Boyke");
        snapshot.PublicRepos.Should().Be(19);
        snapshot.Followers.Should().Be(28);
        snapshot.Stars.Should().Be(84);
        snapshot.LicenseSpdx.Should().Be("AGPL-3.0");
        snapshot.AuthorAvatarUri.Host.Should().Be("avatars.githubusercontent.com");
    }

    [Fact]
    public async Task FetchSnapshotAsync_WhenOfflineOrRateLimited_ShouldReturnInstantFallbackWithoutThrowing()
    {
        using HttpClient httpClient = new(MockHttpMessageHandler.WithException(new HttpRequestException("Offline")));
        GitHubRepoStatsClient client = new(httpClient);

        GitHubAboutSnapshot snapshot = await client.FetchSnapshotAsync();

        snapshot.IsLive.Should().BeFalse();
        snapshot.AuthorDisplayName.Should().Be("Tobias Boyke");
        snapshot.AuthorHandle.Should().Be("@T-Boyke");
        snapshot.LicenseSpdx.Should().Be("AGPL-3.0");
        snapshot.AuthorProfileUri.Should().Be(new Uri("https://github.com/T-Boyke"));
        snapshot.RepositoryUri.Should().Be(new Uri("https://github.com/T-Boyke/burroQueue"));
    }
}

