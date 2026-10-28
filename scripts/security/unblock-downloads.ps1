# scripts/security/unblock-downloads.ps1
# Entfernt den Windows 'Zone.Identifier' (Mark-of-the-Web) von heruntergeladenen burroQueue-Dateien.
[CmdletBinding()]
param (
    [Parameter(Mandatory = false)]
    [string]$TargetFolder = "$HOME\Downloads"
)

Write-Host "[*] Suche nach burroQueue-Dateien in '$TargetFolder'..." -ForegroundColor Cyan

Get-ChildItem -Path $TargetFolder -Filter "*burroQueue*" -Recurse -File | ForEach-Object {
    Write-Host "  -> Entblocke: $($_.FullName)" -ForegroundColor Yellow
    Unblock-File -Path $_.FullName
}

Write-Host "[OK] Alle burroQueue-Dateien wurden erfolgreich entblockt!" -ForegroundColor Green
