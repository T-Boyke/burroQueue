<#
.SYNOPSIS
    Calculates the next auto-incrementing Semantic Version tag (vMajor.Minor.Patch)
    and generates a comprehensive, categorized Markdown Changelog for GitHub Releases.
#>
param(
    [string]$OutputPath = "./publish-installer/RELEASE_NOTES.md",
    [string]$ChecksumsPath = "./publish-installer/SHA256SUMS.txt",
    [string]$OverrideVersion = "",
    [string]$RepoUrl = "https://github.com/T-Boyke/burroQueue",
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

# 1. Discover existing v*.*.* SemVer tags
$rawTags = @(git tag --list "v*.*.*" 2>$null)
$parsedTags = @()
foreach ($t in $rawTags) {
    $clean = $t.Trim()
    if ($clean -match '^v(\d+\.\d+\.\d+)$') {
        try {
            $ver = [version]$Matches[1]
            $parsedTags += [PSCustomObject]@{
                Tag     = $clean
                Version = $ver
            }
        }
        catch {
            # Ignore non-SemVer tags
        }
    }
}

$sortedTags = @($parsedTags | Sort-Object -Property Version -Descending)
$prevTagObj = if ($sortedTags.Count -gt 0) { $sortedTags[0] } else { $null }
$prevTag = if ($null -ne $prevTagObj) { $prevTagObj.Tag } else { "" }

# 2. Gather commits since previous tag (or full history for initial release)
$revRange = if ([string]::IsNullOrWhiteSpace($prevTag)) { "HEAD" } else { "$prevTag..HEAD" }
$rawCommits = @(git log $revRange --pretty=format:"%H|%h|%s|%an|%ad" --date=short 2>$null)

# 3. Calculate next auto-incrementing SemVer
if (-not [string]::IsNullOrWhiteSpace($OverrideVersion)) {
    $nextVersion = $OverrideVersion.TrimStart('v')
}
elseif ($null -eq $prevTagObj) {
    $nextVersion = "2.1.0"
}
else {
    $major = $prevTagObj.Version.Major
    $minor = $prevTagObj.Version.Minor
    $patch = $prevTagObj.Version.Build

    $hasBreaking = $false
    $hasFeature = $false

    foreach ($line in $rawCommits) {
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        $parts = $line -split '\|', 5
        if ($parts.Count -lt 3) { continue }
        $subject = $parts[2]
        if ($subject -match 'BREAKING CHANGE' -or $subject -match '^[a-z]+(\([^)]+\))?!:') {
            $hasBreaking = $true
        }
        elseif ($subject -match '^feat(\([^)]+\))?:') {
            $hasFeature = $true
        }
    }

    if ($hasBreaking) {
        $major++
        $minor = 0
        $patch = 0
    }
    elseif ($hasFeature) {
        $minor++
        $patch = 0
    }
    else {
        $patch++
    }

    $nextVersion = "$major.$minor.$patch"
}

$nextTag = "v$nextVersion"
$headShaShort = (git rev-parse --short HEAD 2>$null).Trim()
$headShaFull = (git rev-parse HEAD 2>$null).Trim()
$releaseDate = (Get-Date).ToUniversalTime().ToString("yyyy-MM-dd HH:mm 'UTC'")

# 4. Categorize commits according to Conventional Commits 1.0.0
$features = [System.Collections.Generic.List[string]]::new()
$perf = [System.Collections.Generic.List[string]]::new()
$fixes = [System.Collections.Generic.List[string]]::new()
$refactors = [System.Collections.Generic.List[string]]::new()
$cicd = [System.Collections.Generic.List[string]]::new()
$testsAndDocs = [System.Collections.Generic.List[string]]::new()
$other = [System.Collections.Generic.List[string]]::new()

foreach ($line in $rawCommits) {
    if ([string]::IsNullOrWhiteSpace($line)) { continue }
    $parts = $line -split '\|', 5
    if ($parts.Count -lt 5) { continue }

    $fullSha = $parts[0]
    $shortSha = $parts[1]
    $subject = $parts[2]
    $date = $parts[4]

    $scopePrefix = ""
    $cleanSubject = $subject

    if ($subject -match '^(?<type>[a-z]+)(\((?<scope>[^)]+)\))?!?: (?<desc>.+)$') {
        $type = $Matches['type'].ToLowerInvariant()
        $scope = $Matches['scope']
        $desc = $Matches['desc']
        if (-not [string]::IsNullOrWhiteSpace($scope)) {
            $scopePrefix = "**``$scope``**: "
        }
        $cleanSubject = $desc
    }
    else {
        $type = "other"
    }

    $entry = "- $scopePrefix$cleanSubject ([``$shortSha``]($RepoUrl/commit/$fullSha)) _($date)_"

    switch -Regex ($type) {
        '^feat$'                 { $features.Add($entry); break }
        '^perf$'                 { $perf.Add($entry); break }
        '^fix$'                  { $fixes.Add($entry); break }
        '^refactor$'             { $refactors.Add($entry); break }
        '^(ci|build|chore)$'     { $cicd.Add($entry); break }
        '^(test|docs|style)$'    { $testsAndDocs.Add($entry); break }
        default                  { $other.Add($entry); break }
    }
}

$totalCommitsInRelease = $rawCommits.Count

# 5. Build Markdown Changelog (using codepoint escapes so both PowerShell 5.1 and pwsh 7 parse without BOM issues)
$iconDonkey   = [char]::ConvertFromUtf32(0x1FADF)
$iconPackage  = [char]::ConvertFromUtf32(0x1F4E6)
$iconLock     = [char]::ConvertFromUtf32(0x1F510)
$iconBoard    = [char]::ConvertFromUtf32(0x1F4CB)
$iconRocket   = [char]::ConvertFromUtf32(0x1F680)
$iconZap      = [char]::ConvertFromUtf32(0x26A1)
$iconBug      = [char]::ConvertFromUtf32(0x1F41B)
$iconPillar   = [char]::ConvertFromUtf32(0x1F3DB)
$iconShield   = [char]::ConvertFromUtf32(0x1F6E1)
$iconTest     = [char]::ConvertFromUtf32(0x1F9EA)
$iconPin      = [char]::ConvertFromUtf32(0x1F4CC)
$emDash       = [char]0x2014
$midDot       = [char]0x00B7
$rightArrow   = [char]0x2192

$sb = [System.Text.StringBuilder]::new()
[void]$sb.AppendLine("## $iconDonkey burroQueue $nextTag $emDash Windows 11 Collaborative Office Jukebox")
[void]$sb.AppendLine("")
[void]$sb.AppendLine("> **Build-Datum:** ``$releaseDate`` $midDot **Commit:** [``$headShaShort``]($RepoUrl/commit/$headShaFull) $midDot **Stack:** ``.NET 11 (win-x64)`` $midDot ``WPF-UI Fluent`` $midDot ``UnoCSS 66.12.0`` $midDot **Lizenz:** ``AGPL-3.0-only``")
[void]$sb.AppendLine("")
[void]$sb.AppendLine("### $iconPackage Downloads & Installation")
[void]$sb.AppendLine("")
[void]$sb.AppendLine("| Datei | Typ | Beschreibung |")
[void]$sb.AppendLine("| :--- | :--- | :--- |")
[void]$sb.AppendLine("| **``burroQueue-Setup-x64.exe``** | Windows 11 Installer (Inno Setup) | Empfohlene Installation inkl. Startmenue-Verknuepfung & optionalem Autostart (User-Scope, keine Admin-Rechte noetig). |")
[void]$sb.AppendLine("| **``burroQueue-portable-win-x64.zip``** | Portable Single-File ZIP | Direkt ausfuehrbare ``burroQueue.exe`` (Self-Contained .NET 11 ``win-x64``, < 30 MB Standby-RAM). |")
[void]$sb.AppendLine("| **``SHA256SUMS.txt``** | Kryptografische Pruefsummen | SHA-256 Hashes zur Verifikation der heruntergeladenen Release-Binaries. |")
[void]$sb.AppendLine("")

if (Test-Path $ChecksumsPath) {
    $checksumLines = @(Get-Content $ChecksumsPath | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    if ($checksumLines.Count -gt 0) {
        [void]$sb.AppendLine("#### $iconLock SHA-256 Checksums")
        [void]$sb.AppendLine("")
        [void]$sb.AppendLine('```text')
        foreach ($cl in $checksumLines) {
            [void]$sb.AppendLine($cl)
        }
        [void]$sb.AppendLine('```')
        [void]$sb.AppendLine("")
    }
}

[void]$sb.AppendLine("---")
[void]$sb.AppendLine("")
if ([string]::IsNullOrWhiteSpace($prevTag)) {
    [void]$sb.AppendLine("### $iconBoard Changelog ($totalCommitsInRelease Commits in Release ``$nextTag``)")
}
else {
    [void]$sb.AppendLine("### $iconBoard Changelog (``$prevTag`` $rightArrow ``$nextTag`` $midDot $totalCommitsInRelease Commits)")
}
[void]$sb.AppendLine("")

if ($features.Count -gt 0) {
    [void]$sb.AppendLine("#### $iconRocket Features & New Capabilities (``feat``)")
    [void]$sb.AppendLine("")
    foreach ($item in $features) { [void]$sb.AppendLine($item) }
    [void]$sb.AppendLine("")
}

if ($perf.Count -gt 0) {
    [void]$sb.AppendLine("#### $iconZap Performance & SIMD Optimizations (``perf``)")
    [void]$sb.AppendLine("")
    foreach ($item in $perf) { [void]$sb.AppendLine($item) }
    [void]$sb.AppendLine("")
}

if ($fixes.Count -gt 0) {
    [void]$sb.AppendLine("#### $iconBug Bug Fixes & Stability (``fix``)")
    [void]$sb.AppendLine("")
    foreach ($item in $fixes) { [void]$sb.AppendLine($item) }
    [void]$sb.AppendLine("")
}

if ($refactors.Count -gt 0) {
    [void]$sb.AppendLine("#### $iconPillar Architecture & Refactoring (``refactor``)")
    [void]$sb.AppendLine("")
    foreach ($item in $refactors) { [void]$sb.AppendLine($item) }
    [void]$sb.AppendLine("")
}

if ($cicd.Count -gt 0) {
    [void]$sb.AppendLine("#### $iconShield CI/CD, Release Pipeline & Packaging (``ci`` / ``build`` / ``chore``)")
    [void]$sb.AppendLine("")
    foreach ($item in $cicd) { [void]$sb.AppendLine($item) }
    [void]$sb.AppendLine("")
}

if ($testsAndDocs.Count -gt 0) {
    [void]$sb.AppendLine("#### $iconTest TDD Tests, Documentation & Code Quality (``test`` / ``docs`` / ``style``)")
    [void]$sb.AppendLine("")
    foreach ($item in $testsAndDocs) { [void]$sb.AppendLine($item) }
    [void]$sb.AppendLine("")
}

if ($other.Count -gt 0) {
    [void]$sb.AppendLine("#### $iconPin Other Changes")
    [void]$sb.AppendLine("")
    foreach ($item in $other) { [void]$sb.AppendLine($item) }
    [void]$sb.AppendLine("")
}

[void]$sb.AppendLine("---")
[void]$sb.AppendLine("")
if (-not [string]::IsNullOrWhiteSpace($prevTag)) {
    [void]$sb.AppendLine("**Full Changelog Diff:** [$prevTag...$nextTag]($RepoUrl/compare/$prevTag...$nextTag)")
}
else {
    [void]$sb.AppendLine("**Full Commit History:** [Commits on ``main``]($RepoUrl/commits/main)")
}

$outDir = Split-Path -Parent $OutputPath
if (-not [string]::IsNullOrWhiteSpace($outDir) -and -not (Test-Path $outDir)) {
    New-Item -ItemType Directory -Force -Path $outDir | Out-Null
}

$utf8NoBom = [System.Text.UTF8Encoding]::new($false)
[System.IO.File]::WriteAllText($OutputPath, $sb.ToString(), $utf8NoBom)

Write-Host "PREV_TAG=$prevTag"
Write-Host "NEXT_VERSION=$nextVersion"
Write-Host "NEXT_TAG=$nextTag"
Write-Host "RELEASE_NOTES_PATH=$OutputPath"

if (-not $DryRun -and -not [string]::IsNullOrWhiteSpace($env:GITHUB_OUTPUT)) {
    [System.IO.File]::AppendAllText($env:GITHUB_OUTPUT, "prev_tag=$prevTag`nnext_version=$nextVersion`nnext_tag=$nextTag`nrelease_notes_path=$OutputPath`n", $utf8NoBom)
}
