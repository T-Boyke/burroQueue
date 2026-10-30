<#
.SYNOPSIS
    Automated Release & Packaging Script for burroQueue (.NET 11 & UnoCSS 66.12.0)
.DESCRIPTION
    1. Compiles UnoCSS 66.12.0 atomic WebDrop stylesheet (when npm is installed).
    2. Runs DDD architecture and 1CPF verification.
    3. Executes full test suite in Release configuration.
    4. Publishes self-contained single-file executable for win-x64 (.NET 11).
    5. Optionally signs binaries via signtool.exe if BURROQUEUE_CERT_SHA1 is set.
    6. Copies assets, packages portable ZIP archive, compiles Inno Setup installer, and writes SHA256SUMS.txt.
#>
[CmdletBinding()]
param(
    [switch]$SkipTests
)

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $PSScriptRoot

function Invoke-OptionalLocalAuthenticodeSign([string]$TargetExe) {
    if ([string]::IsNullOrWhiteSpace($env:BURROQUEUE_CERT_SHA1)) {
        return
    }
    $signtool = Get-Command signtool.exe -ErrorAction SilentlyContinue
    if (-not $signtool) {
        Write-Warning "BURROQUEUE_CERT_SHA1 is set, but signtool.exe was not found in PATH."
        return
    }
    Write-Host "Signing $TargetExe with certificate $($env:BURROQUEUE_CERT_SHA1) ..." -ForegroundColor Cyan
    & $signtool.Source sign /sha1 $env:BURROQUEUE_CERT_SHA1 /fd SHA256 /tr "http://timestamp.digicert.com" /td SHA256 $TargetExe
    if ($LASTEXITCODE -ne 0) {
        throw "Authenticode signing failed for $TargetExe (exit code $LASTEXITCODE)."
    }
}

Write-Host "=== [0/5] Building UnoCSS 66.12.0 WebDrop Stylesheet ===" -ForegroundColor Cyan
$NpmCmd = Get-Command npm -ErrorAction SilentlyContinue
if ($NpmCmd -and (Test-Path "$RepoRoot/src/burroQueue.WebDrop/package.json")) {
    Push-Location "$RepoRoot/src/burroQueue.WebDrop"
    try {
        if (-not (Test-Path "node_modules")) {
            & npm ci
        }
        & npm run build:css
        if ($LASTEXITCODE -ne 0) {
            throw "UnoCSS build:css failed!"
        }
    } finally {
        Pop-Location
    }
} else {
    Write-Host "[INFO] npm not found; using committed webdrop.uno.min.css embedded resource." -ForegroundColor Yellow
}

Write-Host "=== [1/5] Verifying DDD Boundaries & 1CPF Architecture ===" -ForegroundColor Cyan
& pwsh "$RepoRoot/scripts/verify-architecture.ps1"
if ($LASTEXITCODE -ne 0) {
    Write-Error "Architecture verification failed!"
    exit 1
}

if (-not $SkipTests) {
    Write-Host "=== [2/5] Running TDD Unit & Integration Tests (.NET 11 Release) ===" -ForegroundColor Cyan
    & dotnet test "$RepoRoot/burroQueue.sln" --configuration Release --verbosity minimal
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Test suite execution failed!"
        exit 1
    }
} else {
    Write-Host "=== [2/5] Skipping Tests as requested ===" -ForegroundColor Yellow
}

Write-Host "=== [3/5] Publishing Self-Contained Win-x64 Single-File Executable (.NET 11) ===" -ForegroundColor Cyan
$PublishDir = "$RepoRoot/publish/burroQueue-win-x64"
$InstallerDir = "$RepoRoot/publish-installer"

if (Test-Path $PublishDir) { Remove-Item -Recurse -Force $PublishDir }
if (-not (Test-Path $InstallerDir)) { New-Item -ItemType Directory -Force -Path $InstallerDir | Out-Null }

& dotnet publish "$RepoRoot/src/burroQueue.Presentation/burroQueue.Presentation.csproj" `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=none `
    --output $PublishDir

if ($LASTEXITCODE -ne 0) {
    Write-Error "Dotnet publish failed!"
    exit 1
}

Invoke-OptionalLocalAuthenticodeSign "$PublishDir/burroQueue.exe"

# Assets kopieren
$AssetsDir = "$PublishDir/Assets"
New-Item -ItemType Directory -Force -Path $AssetsDir | Out-Null
Copy-Item "$RepoRoot/assets/app.ico", "$RepoRoot/assets/logo.png" -Destination "$AssetsDir/" -Force
Copy-Item "$RepoRoot/LICENSE", "$RepoRoot/README.md" -Destination "$PublishDir/" -Force

# Portable Copy für Direktstart
$PortableDir = "$RepoRoot/publish/portable"
New-Item -ItemType Directory -Force -Path $PortableDir | Out-Null
Copy-Item "$PublishDir/burroQueue.exe" -Destination "$PortableDir/burroQueue.exe" -Force

Write-Host "=== [4/5] Packaging Portable ZIP Archive ===" -ForegroundColor Cyan
$ZipPath = "$InstallerDir/burroQueue-portable-win-x64.zip"
if (Test-Path $ZipPath) { Remove-Item -Force $ZipPath }
Compress-Archive -Path "$PublishDir/*" -DestinationPath $ZipPath -Force
Write-Host "[OK] Portable ZIP created: $ZipPath" -ForegroundColor Green

Write-Host "=== [5/5] Inno Setup Installer Compilation ===" -ForegroundColor Cyan
$IsccCmd = Get-Command iscc -ErrorAction SilentlyContinue
$IsccPath = $null
if ($IsccCmd) {
    $IsccPath = $IsccCmd.Source
} elseif (Test-Path "C:\Program Files (x86)\Inno Setup 6\iscc.exe") {
    $IsccPath = "C:\Program Files (x86)\Inno Setup 6\iscc.exe"
} elseif (Test-Path "C:\Program Files\Inno Setup 6\iscc.exe") {
    $IsccPath = "C:\Program Files\Inno Setup 6\iscc.exe"
}

if ($IsccPath) {
    Write-Host "Compiling installer using $IsccPath ..." -ForegroundColor Cyan
    & $IsccPath "$RepoRoot/scripts/installer/burroQueue.iss"
    if ($LASTEXITCODE -eq 0) {
        Invoke-OptionalLocalAuthenticodeSign "$InstallerDir/burroQueue-Setup-x64.exe"
        Write-Host "[OK] Installer successfully built in $InstallerDir/burroQueue-Setup-x64.exe" -ForegroundColor Green
    } else {
        Write-Warning "Inno Setup compilation returned exit code $LASTEXITCODE"
    }
} else {
    Write-Host "[INFO] Inno Setup (iscc.exe) not found locally. (Installer will be generated and signed via SignPath.io in GitHub Actions on Release tags)." -ForegroundColor Yellow
}

$ChecksumFile = "$InstallerDir/SHA256SUMS.txt"
Get-ChildItem $InstallerDir -File -Include *.exe, *.zip | ForEach-Object {
    $hash = (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $($_.Name)"
} | Set-Content -Path $ChecksumFile -Encoding utf8NoBOM

Write-Host "`n[SUCCESS] burroQueue Release Build Completed Successfully!" -ForegroundColor Green
Write-Host "Portable Exe: $PublishDir\burroQueue.exe" -ForegroundColor Gray
Write-Host "Portable Zip: $ZipPath" -ForegroundColor Gray
Write-Host "Checksums:    $ChecksumFile" -ForegroundColor Gray
