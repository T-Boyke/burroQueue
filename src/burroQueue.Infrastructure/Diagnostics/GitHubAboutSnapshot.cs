namespace burroQueue.Infrastructure.Diagnostics;

public sealed record GitHubAboutSnapshot(
    string AuthorDisplayName,
    string AuthorHandle,
    Uri AuthorProfileUri,
    Uri AuthorAvatarUri,
    Uri RepositoryUri,
    int PublicRepos,
    int Followers,
    int Stars,
    string LicenseSpdx,
    string AppVersion,
    bool IsLive)
{
    public static GitHubAboutSnapshot DefaultFallback(string appVersion = "1.1.0") =>
        new(
            AuthorDisplayName: "Tobias Boyke",
            AuthorHandle: "@T-Boyke",
            AuthorProfileUri: new Uri("https://github.com/T-Boyke"),
            AuthorAvatarUri: new Uri("https://avatars.githubusercontent.com/u/37149164?v=4&s=72"),
            RepositoryUri: new Uri("https://github.com/T-Boyke/burroQueue"),
            PublicRepos: 17,
            Followers: 24,
            Stars: 76,
            LicenseSpdx: "AGPL-3.0",
            AppVersion: appVersion,
            IsLive: false);
}

