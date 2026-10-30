using System.Diagnostics;
using FluentAssertions;
using Xunit;

namespace burroQueue.Tests.Infrastructure;

public sealed class ReleaseWorkflowValidationTests
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
    public void ReleaseWorkflow_ShouldTriggerOnGreenCiWorkflowRun_AndAvoidSecretsInJobLevelIf()
    {
        string releaseYmlPath = Path.Combine(GetRepoRoot(), ".github", "workflows", "release.yml");
        File.Exists(releaseYmlPath).Should().BeTrue();

        string content = File.ReadAllText(releaseYmlPath);

        content.Should().Contain("workflow_run:");
        content.Should().Contain("burroQueue CI");
        content.Should().Contain("generate-release-notes.ps1");
        content.Should().Contain("SignPath/github-action-submit-signing-request@v3");
        content.Should().Contain("-p:IncludeAllContentForSelfExtract=true");
        content.Should().NotContain("-p:IncludeNativeLibrariesForSelfExtract=true");
        content.Should().NotContain("SignPath/github-action-submit-signing-request@v2");
        content.Should().NotContain("Get-ChildItem ./publish-installer -File -Include");
        content.Should().NotContain("if: startsWith(github.ref, 'refs/tags/v') && secrets.");
    }

    [Fact]
    public void GenerateReleaseNotesScript_ShouldCalculateNextVersionAndFormatDetailedChangelog()
    {
        string repoRoot = GetRepoRoot();
        string scriptPath = Path.Combine(repoRoot, "scripts", "generate-release-notes.ps1");
        File.Exists(scriptPath).Should().BeTrue("generate-release-notes.ps1 must exist for automated CI releases");

        string tempOutput = Path.Combine(Path.GetTempPath(), $"burroQueue-release-notes-{Guid.NewGuid():N}.md");
        try
        {
            ProcessStartInfo psi = new()
            {
                FileName = "powershell",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\" -OutputPath \"{tempOutput}\" -DryRun",
                WorkingDirectory = repoRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using Process? process = Process.Start(psi);
            process.Should().NotBeNull();
            string stdout = process!.StandardOutput.ReadToEnd();
            string stderr = process.StandardError.ReadToEnd();
            process.WaitForExit(15000).Should().BeTrue();
            process.ExitCode.Should().Be(0, $"stderr: {stderr}");

            stdout.Should().Contain("NEXT_TAG=v");
            File.Exists(tempOutput).Should().BeTrue();

            string notes = File.ReadAllText(tempOutput);
            notes.Should().Contain("## ");
            notes.Should().Contain("Changelog");
            notes.Should().Contain("burroQueue-Setup-x64.exe");
        }
        finally
        {
            if (File.Exists(tempOutput))
            {
                File.Delete(tempOutput);
            }
        }
    }
}
