using System.Net.Http;
using System.Text.Json;

namespace burroQueue.Infrastructure.Diagnostics;

public sealed class GitHubRepoStatsClient : IGitHubRepoStatsClient, IDisposable
{
    private const string UserApiUrl = "https://api.github.com/users/T-Boyke";
    private const string RepoApiUrl = "https://api.github.com/repos/T-Boyke/burroQueue";

    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;

    public GitHubRepoStatsClient(HttpClient? httpClient = null)
    {
        _ownsHttpClient = httpClient is null;
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
    }

    public async Task<GitHubAboutSnapshot> FetchSnapshotAsync(CancellationToken cancellationToken = default)
    {
        GitHubAboutSnapshot fallback = GitHubAboutSnapshot.DefaultFallback();

        try
        {
            using CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(3));

            Task<HttpResponseMessage> userTask = SendGitHubGetAsync(UserApiUrl, timeoutCts.Token);
            Task<HttpResponseMessage> repoTask = SendGitHubGetAsync(RepoApiUrl, timeoutCts.Token);

            await Task.WhenAll(userTask, repoTask).ConfigureAwait(false);

            using HttpResponseMessage userResp = await userTask.ConfigureAwait(false);
            using HttpResponseMessage repoResp = await repoTask.ConfigureAwait(false);

            if (!userResp.IsSuccessStatusCode || !repoResp.IsSuccessStatusCode)
            {
                return fallback;
            }

            string userJson = await userResp.Content.ReadAsStringAsync(timeoutCts.Token).ConfigureAwait(false);
            string repoJson = await repoResp.Content.ReadAsStringAsync(timeoutCts.Token).ConfigureAwait(false);

            using JsonDocument userDoc = JsonDocument.Parse(userJson);
            using JsonDocument repoDoc = JsonDocument.Parse(repoJson);

            JsonElement userRoot = userDoc.RootElement;
            JsonElement repoRoot = repoDoc.RootElement;

            string displayName = userRoot.TryGetProperty("name", out JsonElement nameEl) && nameEl.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(nameEl.GetString())
                ? nameEl.GetString()!
                : fallback.AuthorDisplayName;

            string login = userRoot.TryGetProperty("login", out JsonElement loginEl) && loginEl.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(loginEl.GetString())
                ? $"@{loginEl.GetString()!.TrimStart('@')}"
                : fallback.AuthorHandle;

            Uri avatarUri = fallback.AuthorAvatarUri;
            if (userRoot.TryGetProperty("avatar_url", out JsonElement avatarEl) &&
                avatarEl.ValueKind == JsonValueKind.String &&
                Uri.TryCreate(avatarEl.GetString(), UriKind.Absolute, out Uri? parsedAvatar) &&
                parsedAvatar.Scheme == Uri.UriSchemeHttps)
            {
                avatarUri = parsedAvatar;
            }

            int publicRepos = userRoot.TryGetProperty("public_repos", out JsonElement reposEl) && reposEl.TryGetInt32(out int rCount)
                ? rCount
                : fallback.PublicRepos;

            int followers = userRoot.TryGetProperty("followers", out JsonElement folEl) && folEl.TryGetInt32(out int fCount)
                ? fCount
                : fallback.Followers;

            int stars = repoRoot.TryGetProperty("stargazers_count", out JsonElement starEl) && starEl.TryGetInt32(out int sCount)
                ? sCount
                : fallback.Stars;

            string licenseSpdx = fallback.LicenseSpdx;
            if (repoRoot.TryGetProperty("license", out JsonElement licEl) &&
                licEl.ValueKind == JsonValueKind.Object &&
                licEl.TryGetProperty("spdx_id", out JsonElement spdxEl) &&
                spdxEl.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(spdxEl.GetString()))
            {
                licenseSpdx = spdxEl.GetString()!;
            }

            return new GitHubAboutSnapshot(
                AuthorDisplayName: displayName,
                AuthorHandle: login,
                AuthorProfileUri: fallback.AuthorProfileUri,
                AuthorAvatarUri: avatarUri,
                RepositoryUri: fallback.RepositoryUri,
                PublicRepos: publicRepos,
                Followers: followers,
                Stars: stars,
                LicenseSpdx: licenseSpdx,
                AppVersion: fallback.AppVersion,
                IsLive: true);
        }
        catch
        {
            return fallback;
        }
    }

    private Task<HttpResponseMessage> SendGitHubGetAsync(string url, CancellationToken cancellationToken)
    {
        HttpRequestMessage req = new(HttpMethod.Get, url);
        req.Headers.TryAddWithoutValidation("User-Agent", "burroQueue/1.1");
        req.Headers.TryAddWithoutValidation("Accept", "application/vnd.github+json");
        return _httpClient.SendAsync(req, cancellationToken);
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }
}
