namespace burroQueue.Infrastructure.Diagnostics;

public interface IGitHubRepoStatsClient
{
    Task<GitHubAboutSnapshot> FetchSnapshotAsync(CancellationToken cancellationToken = default);
}

