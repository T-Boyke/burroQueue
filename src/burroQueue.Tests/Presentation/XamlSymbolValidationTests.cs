using System.IO;
using System.Text.RegularExpressions;
using FluentAssertions;
using Wpf.Ui.Controls;
using Xunit;

namespace burroQueue.Tests.Presentation;

public sealed class XamlSymbolValidationTests
{
    private static readonly Regex _symbolRegex = new(@"(?:Symbol\s*=\s*|Property\s*=\s*""Symbol""\s+Value\s*=\s*)""([^""]+)""", RegexOptions.Compiled);

    [Fact]
    public void AllXamlSymbols_ShouldBeValidSymbolRegularValues()
    {
        string solutionRoot = FindSolutionRoot();
        string presentationDir = Path.Combine(solutionRoot, "src", "burroQueue.Presentation");

        string[] xamlFiles = Directory.GetFiles(presentationDir, "*.xaml", SearchOption.AllDirectories);
        xamlFiles.Should().NotBeEmpty("Presentation project must contain XAML files");

        int validatedCount = 0;
        List<string> invalidSymbols = [];

        foreach (string file in xamlFiles)
        {
            string content = File.ReadAllText(file);
            MatchCollection matches = _symbolRegex.Matches(content);

            foreach (Match match in matches)
            {
                string symbolName = match.Groups[1].Value;

                // Ignore bindings or template expressions if any
                if (symbolName.StartsWith('{') || string.IsNullOrWhiteSpace(symbolName))
                {
                    continue;
                }

                validatedCount++;
                if (!IsValidSymbol(symbolName))
                {
                    invalidSymbols.Add($"{Path.GetFileName(file)}: '{symbolName}'");
                }
            }
        }

        validatedCount.Should().BeGreaterThan(0, "At least one SymbolIcon should be validated");
        invalidSymbols.Should().BeEmpty("All SymbolIcon Symbol attributes must map to valid SymbolRegular enum values");
    }

    [Theory]
    [InlineData("MusicNote224", true)]
    [InlineData("PeopleCommunity24", true)]
    [InlineData("ArrowDownload24", true)]
    [InlineData("MusicNote24", false)]
    [InlineData("NonExistentSymbol999", false)]
    public void IsValidSymbol_ShouldCorrectlyValidateSymbols(string symbolName, bool expectedValid)
    {
        bool actual = IsValidSymbol(symbolName);
        actual.Should().Be(expectedValid);
    }

    internal static bool IsValidSymbol(string symbolName)
    {
        return Enum.TryParse<SymbolRegular>(symbolName, false, out _);
    }

    internal static string FindSolutionRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "burroQueue.sln")))
        {
            current = current.Parent;
        }

        return current?.FullName ?? throw new DirectoryNotFoundException("Solution root could not be located");
    }
}
