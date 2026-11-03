<#
.SYNOPSIS
    Generates official WinGet manifests for microsoft/winget-pkgs.
    Output conforms to the community repo structure:
    manifests/t/T-Boyke/burroQueue/<version>/
#>
param(
    [Parameter(Mandatory=$true)]
    [string]$Version,
    [string]$InstallerUrl = "",
    [string]$InstallerSha256 = "",
    [string]$OutputDir = ""
)

$ErrorActionPreference = "Stop"

$cleanVer = $Version.TrimStart('v')
if ([string]::IsNullOrWhiteSpace($InstallerUrl)) {
    $InstallerUrl = "https://github.com/T-Boyke/burroQueue/releases/download/v$cleanVer/burroQueue-Setup-x64.exe"
}

if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $OutputDir = Join-Path $PSScriptRoot "manifests/t/T-Boyke/burroQueue/$cleanVer"
}

if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
}

$utf8NoBom = [System.Text.UTF8Encoding]::new($false)

# 1. Version Manifest
$versionYaml = @"
# yaml-language-server: `$schema=https://aka.ms/winget-manifest.version.1.6.0.schema.json
PackageIdentifier: T-Boyke.burroQueue
PackageVersion: $cleanVer
DefaultLocale: en-US
ManifestType: version
ManifestVersion: 1.6.0
"@
[System.IO.File]::WriteAllText((Join-Path $OutputDir "T-Boyke.burroQueue.yaml"), $versionYaml, $utf8NoBom)

# 2. Installer Manifest
$installerYaml = @"
# yaml-language-server: `$schema=https://aka.ms/winget-manifest.installer.1.6.0.schema.json
PackageIdentifier: T-Boyke.burroQueue
PackageVersion: $cleanVer
MinimumOSVersion: 10.0.22000.0
InstallerType: inno
Scope: user
InstallModes:
  - interactive
  - silent
  - silentWithProgress
UpgradeBehavior: install
Installers:
  - Architecture: x64
    InstallerUrl: $InstallerUrl
    InstallerSha256: $InstallerSha256
ManifestType: installer
ManifestVersion: 1.6.0
"@
[System.IO.File]::WriteAllText((Join-Path $OutputDir "T-Boyke.burroQueue.installer.yaml"), $installerYaml, $utf8NoBom)

# 3. Default Locale (en-US)
$localeEnYaml = @"
# yaml-language-server: `$schema=https://aka.ms/winget-manifest.defaultLocale.1.6.0.schema.json
PackageIdentifier: T-Boyke.burroQueue
PackageVersion: $cleanVer
PackageLocale: en-US
Publisher: Tobias Boyke (@T-Boyke)
PublisherUrl: https://github.com/T-Boyke
PackageName: burroQueue
PackageUrl: https://github.com/T-Boyke/burroQueue
License: AGPL-3.0-only
LicenseUrl: https://github.com/T-Boyke/burroQueue/blob/main/LICENSE
ShortDescription: Collaborative Windows 11 Office Jukebox for YouTube links with VoIP auto-ducking, LAN sync and Mobile WebDrop.
Description: |-
  burroQueue is a native Windows 11 collaborative office jukebox.
  Colleagues can queue YouTube music links via Drag-and-Drop, global clipboard monitoring,
  local network auto-discovery, or the embedded mobile WebDrop server with playback controls.
  Features intelligent VoIP call ducking (SwyxIt, Starface, Windows CoreAudio),
  MMCSS audio scheduling, and high-efficiency standby consumption under 30 MB RAM.
Tags:
  - jukebox
  - youtube
  - office
  - audio
  - windows11
ManifestType: defaultLocale
ManifestVersion: 1.6.0
"@
[System.IO.File]::WriteAllText((Join-Path $OutputDir "T-Boyke.burroQueue.locale.en-US.yaml"), $localeEnYaml, $utf8NoBom)

# 4. German Locale (de-DE)
$localeDeYaml = @"
# yaml-language-server: `$schema=https://aka.ms/winget-manifest.locale.1.6.0.schema.json
PackageIdentifier: T-Boyke.burroQueue
PackageVersion: $cleanVer
PackageLocale: de-DE
Publisher: Tobias Boyke (@T-Boyke)
PublisherUrl: https://github.com/T-Boyke
PackageName: burroQueue
PackageUrl: https://github.com/T-Boyke/burroQueue
License: AGPL-3.0-only
LicenseUrl: https://github.com/T-Boyke/burroQueue/blob/main/LICENSE
ShortDescription: Kollaborative Windows 11 Buero-Jukebox fuer YouTube-Links mit VoIP Auto-Ducking, LAN-Sync und Mobile WebDrop.
Description: |-
  burroQueue ist eine native Windows 11 Kollaborations-Jukebox fuers Buero.
  Kollegen koennen YouTube-Links per Drag-and-Drop, automatischer Zwischenablagen-Ueberwachung,
  lokaler Netzwerk-Gruppensuche oder ueber den integrierten mobilen WebDrop-Server inklusive
  Wiedergabesteuerung hinzufuegen.
  Bietet intelligentes VoIP Call-Ducking (SwyxIt, Starface, Windows CoreAudio),
  MMCSS Audio-Priorisierung und extrem sparsamen Standby-Speicherbedarf von unter 30 MB RAM.
Tags:
  - jukebox
  - youtube
  - office
  - audio
  - windows11
ManifestType: locale
ManifestVersion: 1.6.0
"@
[System.IO.File]::WriteAllText((Join-Path $OutputDir "T-Boyke.burroQueue.locale.de-DE.yaml"), $localeDeYaml, $utf8NoBom)

Write-Host "Generated WinGet manifests in: $OutputDir"
