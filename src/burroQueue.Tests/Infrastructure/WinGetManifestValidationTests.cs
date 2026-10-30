using FluentAssertions;
using Xunit;

namespace burroQueue.Tests.Infrastructure;

public sealed class WinGetManifestValidationTests
{
    private static string GetRepoRoot()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "burroQueue.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }

    [Fact]
    public void WinGetManifests_ShouldExistAndDeclarePackageIdentifierAndAgplLicense()
    {
        string wingetDir = Path.Combine(GetRepoRoot(), "scripts", "winget");
        string versionFile = Path.Combine(wingetDir, "T-Boyke.burroQueue.yaml");
        string installerFile = Path.Combine(wingetDir, "T-Boyke.burroQueue.installer.yaml");
        string localeDeFile = Path.Combine(wingetDir, "T-Boyke.burroQueue.locale.de-DE.yaml");
        string localeEnFile = Path.Combine(wingetDir, "T-Boyke.burroQueue.locale.en-US.yaml");

        File.Exists(versionFile).Should().BeTrue();
        File.Exists(installerFile).Should().BeTrue();
        File.Exists(localeDeFile).Should().BeTrue();
        File.Exists(localeEnFile).Should().BeTrue();

        File.ReadAllText(versionFile).Should().Contain("PackageIdentifier: T-Boyke.burroQueue");
        File.ReadAllText(installerFile).Should().Contain("InstallerType: inno").And.Contain("Architecture: x64");
        File.ReadAllText(localeDeFile).Should().Contain("License: AGPL-3.0-only");
        File.ReadAllText(localeEnFile).Should().Contain("License: AGPL-3.0-only");
    }
}

