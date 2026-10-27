<#
.SYNOPSIS
    Deregistriert den lokalen GitHub Actions Runner für burroQueue sauber.
#>
[CmdletBinding()]
param(
    [string]$RunnerDir = "C:\actions-runner-burroQueue",
    [Parameter(Mandatory = $true)]
    [string]$Token
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$configCmd = Join-Path $RunnerDir "config.cmd"
if (-not (Test-Path $configCmd)) {
    Write-Warning "Kein Runner unter $RunnerDir gefunden."
    exit 0
}

Push-Location $RunnerDir
try {
    & $configCmd remove --token $Token
}
finally {
    Pop-Location
}

