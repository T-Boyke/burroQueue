<#
.SYNOPSIS
    Centralized version bumper for burroQueue.
    Updates Directory.Build.props and scripts/installer/burroQueue.iss.
#>
param(
    [string]$NewVersion = "",
    [ValidateSet("Patch", "Minor", "Major")]
    [string]$IncrementType = "Patch",
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

$propsPath = Join-Path $PSScriptRoot "../Directory.Build.props"
$issPath = Join-Path $PSScriptRoot "installer/burroQueue.iss"

if (-not (Test-Path $propsPath)) {
    throw "Directory.Build.props not found at: $propsPath"
}

$propsContent = [System.IO.File]::ReadAllText($propsPath)
if ($propsContent -match '<Version>(\d+\.\d+\.\d+)</Version>') {
    $currentVersion = $Matches[1]
} else {
    $currentVersion = "2.3.2"
}

if ([string]::IsNullOrWhiteSpace($NewVersion)) {
    $v = [version]$currentVersion
    switch ($IncrementType) {
        "Major" { $nextVersion = "$($v.Major + 1).0.0" }
        "Minor" { $nextVersion = "$($v.Major).$($v.Minor + 1).0" }
        "Patch" { $nextVersion = "$($v.Major).$($v.Minor).$($v.Build + 1)" }
    }
} else {
    $nextVersion = $NewVersion.TrimStart('v')
}

Write-Host "Bumping burroQueue version: $currentVersion -> $nextVersion"

if (-not $DryRun) {
    # 1. Update Directory.Build.props
    if ($propsContent -match '<Version>.*?</Version>') {
        $propsUpdated = [regex]::Replace($propsContent, '<Version>.*?</Version>', "<Version>$nextVersion</Version>")
    } else {
        $propsUpdated = $propsContent -replace '<PropertyGroup>', "<PropertyGroup>`n    <Version>$nextVersion</Version>"
    }
    $utf8NoBom = [System.Text.UTF8Encoding]::new($false)
    [System.IO.File]::WriteAllText($propsPath, $propsUpdated, $utf8NoBom)
    Write-Host "Updated $propsPath"

    # 2. Update scripts/installer/burroQueue.iss
    if (Test-Path $issPath) {
        $issContent = [System.IO.File]::ReadAllText($issPath)
        $issUpdated = [regex]::Replace($issContent, '#define MyAppVersion "[^"]+"', "#define MyAppVersion `"$nextVersion`"")
        [System.IO.File]::WriteAllText($issPath, $issUpdated, $utf8NoBom)
        Write-Host "Updated $issPath"
    }

    if (-not [string]::IsNullOrWhiteSpace($env:GITHUB_OUTPUT)) {
        [System.IO.File]::AppendAllText($env:GITHUB_OUTPUT, "version=$nextVersion`ntag=v$nextVersion`n", $utf8NoBom)
    }
}

