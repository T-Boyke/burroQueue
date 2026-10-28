<#
.SYNOPSIS
    Automated Release & Packaging Script for burroQueue
.DESCRIPTION
    1. Runs architecture and 1CPF verification.
    2. Executes full test suite in Release configuration.
    3. Publishes self-contained single-file executable for win-x64.
    4. Copies assets (app.ico, logo.png, LICENSE, README.md).
    5. Packages portable ZIP archive.
    6. Compiles Inno Setup installer if iscc.exe is available.
#>
[CmdletBinding()]
param(
    [switch]$SkipTests
)

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $PSScriptRoot

Write-Host "=== [1/5] Verifying DDD Boundaries & 1CPF Architecture ===" -ForegroundColor Cyan
& pwsh "$RepoRoot/scripts/verify-architecture.ps1"
if ($LASTEXITCODE -ne 0) {
    Write-Error "Architecture verification failed!"
    exit 1
}

if (-not $SkipTests) {
    Write-Host "=== [2/5] Running TDD Unit & Integration Tests (Release) ===" -ForegroundColor Cyan
    & dotnet test "$RepoRoot/burroQueue.sln" --configuration Release --verbosity minimal
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Test suite execution failed!"
        exit 1
    }
} else {
    Write-Host "=== [2/5] Skipping Tests as requested ===" -ForegroundColor Yellow
}

Write-Host "=== [3/5] Publishing Self-Contained Win-x64 Single-File Executable ===" -ForegroundColor Cyan
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
        Write-Host "[OK] Installer successfully built in $InstallerDir/burroQueue-Setup-x64.exe" -ForegroundColor Green
    } else {
        Write-Warning "Inno Setup compilation returned exit code $LASTEXITCODE"
    }
} else {
    Write-Host "[INFO] Inno Setup (iscc.exe) not found locally. (Installer will be generated automatically in GitHub Actions on Release tags)." -ForegroundColor Yellow
}

Write-Host "`n[SUCCESS] burroQueue 1.0 Release Build Completed Successfully!" -ForegroundColor Green
Write-Host "Portable Exe: $PublishDir\burroQueue.exe" -ForegroundColor Gray
Write-Host "Portable Zip: $ZipPath" -ForegroundColor Gray
