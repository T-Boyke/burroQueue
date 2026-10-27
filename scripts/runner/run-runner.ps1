<#
.SYNOPSIS
    Startet den lokalen GitHub Actions Runner für burroQueue im interaktiven Modus.
#>
[CmdletBinding()]
param(
    [string]$RunnerDir = "C:\actions-runner-burroQueue"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$runCmd = Join-Path $RunnerDir "run.cmd"
if (-not (Test-Path $runCmd)) {
    Write-Error "Runner nicht gefunden unter '$runCmd'. Bitte führe zuerst './scripts/runner/setup-runner.ps1' aus."
    exit 1
}

Write-Host "Starte lokalen GitHub Actions Runner aus $RunnerDir ..." -ForegroundColor Cyan
& $runCmd

