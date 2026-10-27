<#
.SYNOPSIS
    Lädt den offiziellen GitHub Actions Runner für Windows x64 herunter und registriert ihn
    für das Repository T-Boyke/burroQueue.

.DESCRIPTION
    Dieses Skript richtet einen lokalen Self-Hosted GitHub Actions Runner außerhalb des Git-Trees
    (Standard: C:\actions-runner-burroQueue) ein.
    Wenn GitHub CLI (gh) installiert und eingeloggt ist, kann das Registrierungs-Token automatisch
    abgerufen werden. Alternativ kann -Token direkt übergeben werden.

.EXAMPLE
    ./scripts/runner/setup-runner.ps1 -Token "A1B2C3..."
    ./scripts/runner/setup-runner.ps1 -UseGhCli -RunAsService
#>
[CmdletBinding()]
param(
    [string]$RepositoryUrl = "https://github.com/T-Boyke/burroQueue",
    [string]$RunnerDir = "C:\actions-runner-burroQueue",
    [string]$RunnerName = "$env:COMPUTERNAME-burroQueue",
    [string]$Labels = "self-hosted,Windows,X64,burroQueue,win11-wpf",
    [string]$RunnerVersion = "2.322.0",
    [string]$Token = "",
    [switch]$UseGhCli,
    [switch]$RunAsService
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Write-Host "=== burroQueue Self-Hosted GitHub Actions Runner Setup ===" -ForegroundColor Cyan

if (-not (Test-Path $RunnerDir)) {
    Write-Host "Erstelle Runner-Verzeichnis: $RunnerDir" -ForegroundColor Yellow
    New-Item -ItemType Directory -Path $RunnerDir -Force | Out-Null
}

$zipName = "actions-runner-win-x64-$RunnerVersion.zip"
$zipPath = Join-Path $RunnerDir $zipName
$configCmd = Join-Path $RunnerDir "config.cmd"

if (-not (Test-Path $configCmd)) {
    $downloadUrl = "https://github.com/actions/runner/releases/download/v$RunnerVersion/$zipName"
    Write-Host "Lade GitHub Actions Runner v$RunnerVersion herunter..." -ForegroundColor Cyan
    Invoke-WebRequest -Uri $downloadUrl -OutFile $zipPath -UseBasicParsing

    Write-Host "Entpacke Runner-Archiv nach $RunnerDir ..." -ForegroundColor Cyan
    Expand-Archive -Path $zipPath -DestinationPath $RunnerDir -Force
    Remove-Item -Path $zipPath -Force
} else {
    Write-Host "Runner-Binaries bereits vorhanden in $RunnerDir." -ForegroundColor Green
}

if ([string]::IsNullOrWhiteSpace($Token) -and $UseGhCli) {
    if (Get-Command gh -ErrorAction SilentlyContinue) {
        Write-Host "Frage Registrierungs-Token über GitHub CLI (gh) an..." -ForegroundColor Cyan
        $repoSlug = ($RepositoryUrl -replace "^https://github\.com/", "").TrimEnd("/")
        $Token = (gh api --method POST "repos/$repoSlug/actions/runners/registration-token" --jq ".token").Trim()
    } else {
        Write-Warning "GitHub CLI (gh) ist nicht installiert. Bitte übergib -Token manuell."
    }
}

if ([string]::IsNullOrWhiteSpace($Token)) {
    Write-Host "`nRunner-Binaries sind bereit unter: $RunnerDir" -ForegroundColor Green
    Write-Host "Um die Registrierung abzuschließen, hole ein Token unter:" -ForegroundColor Yellow
    Write-Host "  $RepositoryUrl/settings/actions/runners/new" -ForegroundColor White
    Write-Host "und führe dann aus:" -ForegroundColor Yellow
    Write-Host "  ./scripts/runner/setup-runner.ps1 -Token '<DEIN_TOKEN>'" -ForegroundColor Cyan
    exit 0
}

Push-Location $RunnerDir
try {
    $argsList = @(
        "--url", $RepositoryUrl,
        "--token", $Token,
        "--name", $RunnerName,
        "--labels", $Labels,
        "--work", "_work",
        "--unattended",
        "--replace"
    )
    if ($RunAsService) {
        $argsList += "--runasservice"
    }

    Write-Host "Konfiguriere Runner '$RunnerName' für $RepositoryUrl ..." -ForegroundColor Cyan
    & $configCmd @argsList
    if ($LASTEXITCODE -ne 0) {
        throw "Runner-Konfiguration fehlgeschlagen mit Exit-Code $LASTEXITCODE."
    }

    Write-Host "`n[OK] Self-Hosted Runner erfolgreich registriert!" -ForegroundColor Green
    Write-Host "Starte den Runner jederzeit über: ./scripts/runner/run-runner.ps1" -ForegroundColor Cyan
}
finally {
    Pop-Location
}

