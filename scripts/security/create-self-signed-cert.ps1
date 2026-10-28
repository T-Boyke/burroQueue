# scripts/security/create-self-signed-cert.ps1
# Erstellt ein lokales Code-Signing-Zertifikat und signiert die angegebene Binärdatei.
[CmdletBinding()]
param (
    [Parameter(Mandatory = false)]
    [string]$FilePath = ".\publish\burroQueue-win-x64\burroQueue.exe"
)

$ErrorActionPreference = "Stop"

$certSubject = "CN=burroQueue Dev, O=burroQueue, C=DE"
$cert = Get-ChildItem Cert:\CurrentUser\My | Where-Object { $_.Subject -eq $certSubject } | Select-Object -First 1

if (-not $cert) {
    Write-Host "[*] Erstelle neues lokales Code-Signing-Zertifikat..." -ForegroundColor Cyan
    $cert = New-SelfSignedCertificate `
        -Type CodeSigningCert `
        -Subject $certSubject `
        -KeySpec Signature `
        -KeyAlgorithm RSA `
        -KeyLength 2048 `
        -HashAlgorithm SHA256 `
        -CertStoreLocation "Cert:\CurrentUser\My" `
        -NotAfter (Get-Date).AddYears(5)

    Write-Host "[*] Installiere Zertifikat in vertrauenswürdigen Stammzertifizierungsstellen..." -ForegroundColor Cyan
    $rootStore = New-Object System.Security.Cryptography.X509Certificates.X509Store("Root", "CurrentUser")
    $rootStore.Open("ReadWrite")
    $rootStore.Add($cert)
    $rootStore.Close()
    Write-Host "[OK] Entwickler-Zertifikat erfolgreich installiert!" -ForegroundColor Green
} else {
    Write-Host "[OK] Bestehendes Entwickler-Zertifikat gefunden: $($cert.Thumbprint)" -ForegroundColor Green
}

if (Test-Path $FilePath) {
    Write-Host "[*] Signiere '$FilePath'..." -ForegroundColor Cyan
    Set-AuthenticodeSignature -FilePath $FilePath -Certificate $cert -TimestampServer "http://timestamp.digicert.com"
    Unblock-File -Path $FilePath
    Write-Host "[OK] '$FilePath' erfolgreich signiert und entblockt!" -ForegroundColor Green
} else {
    Write-Host "[!] Datei '$FilePath' nicht gefunden. Bitte Pfad prüfen." -ForegroundColor Yellow
}
