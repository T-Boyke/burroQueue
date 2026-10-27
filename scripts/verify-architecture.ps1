<#
.SYNOPSIS
    Prüft die Architektur-Prinzipien von burroQueue:
    1. Pure Domain Layer (burroQueue.Domain darf keine externen Projekt- oder Paket-Abhängigkeiten besitzen)
    2. SFC / 1CPF (Single Component Per File): Jede .cs-Datei in src/ enthält exakt einen Top-Level-Typ
       und der Dateiname entspricht dem Typnamen.
#>
[CmdletBinding()]
param(
    [string]$RepoRoot = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
    if ($PSScriptRoot) {
        $RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
    }
    else {
        $RepoRoot = (Get-Location).Path
    }
}

$srcPath = Join-Path $RepoRoot "src"
if (-not (Test-Path $srcPath)) {
    Write-Error "Verzeichnis 'src' nicht gefunden unter: $srcPath"
    exit 1
}

$violations = [System.Collections.Generic.List[string]]::new()

Write-Host "=== [1/3] Prüfe Domain-Driven Design (DDD) Reinheit von burroQueue.Domain ===" -ForegroundColor Cyan
$domainCsproj = Join-Path $srcPath "burroQueue.Domain\burroQueue.Domain.csproj"
if (Test-Path $domainCsproj) {
    [xml]$xml = Get-Content -Path $domainCsproj -Raw -Encoding UTF8
    $projectRefs = @($xml.SelectNodes("//ProjectReference"))
    $packageRefs = @($xml.SelectNodes("//PackageReference"))

    if ($projectRefs.Count -gt 0) {
        $violations.Add("DDD-Verstoß: 'burroQueue.Domain.csproj' enthält $($projectRefs.Count) ProjectReference(s). Der Domain-Layer muss komplett eigenständig sein.")
    }
    if ($packageRefs.Count -gt 0) {
        $violations.Add("DDD-Verstoß: 'burroQueue.Domain.csproj' enthält $($packageRefs.Count) PackageReference(s). Der Domain-Layer muss frei von externen Frameworks bleiben.")
    }

    $tfNode = $xml.SelectSingleNode("//TargetFramework")
    if ($null -ne $tfNode -and $tfNode.InnerText -match "-windows") {
        $violations.Add("DDD-Verstoß: 'burroQueue.Domain.csproj' nutzt '$($tfNode.InnerText)'. Die Domain darf nicht an Windows/WPF gekoppelt sein (erwartet: 'net10.0').")
    }
}
else {
    $violations.Add("Struktur-Verstoß: '$domainCsproj' fehlt.")
}

Write-Host "=== [2/3] Prüfe Domain-Dateien auf verbotene UI-/Framework-Namespaces ===" -ForegroundColor Cyan
$domainFiles = Get-ChildItem -Path (Join-Path $srcPath "burroQueue.Domain") -Filter "*.cs" -Recurse -ErrorAction SilentlyContinue |
Where-Object { $_.FullName -notmatch "[\\/](bin|obj)[\\/]" }

foreach ($file in $domainFiles) {
    $content = Get-Content -Path $file.FullName -Raw -Encoding UTF8
    if ($content -match "using\s+(System\.Windows|Wpf\.Ui|CommunityToolkit|System\.Net\.Http|Microsoft\.Extensions)") {
        $violations.Add("DDD-Verstoß in '$($file.Name)': Verbotenes Framework-Using ($($Matches[0])) im Domain-Layer.")
    }
}

Write-Host "=== [3/3] Prüfe SFC / 1CPF (Single Component Per File) in allen Projekten ===" -ForegroundColor Cyan
$csFiles = Get-ChildItem -Path $srcPath -Filter "*.cs" -Recurse |
Where-Object {
    $_.FullName -notmatch "[\\/](bin|obj)[\\/]" -and
    $_.Name -notIn @("AssemblyInfo.cs", "GlobalUsings.cs") -and
    $_.Name -notlike "*.g.cs" -and
    $_.Name -notlike "*.g.i.cs" -and
    $_.Name -notlike "*.Designer.cs"
}

$typeRegex = [regex]::new('(?m)^\s*(?:public|internal|private|protected|file)?\s*(?:readonly\s+|static\s+|abstract\s+|sealed\s+|partial\s+)*(?:record\s+struct|record\s+class|record|class|interface|struct|enum|delegate)\s+([A-Za-z_][A-Za-z0-9_]*)')

foreach ($file in $csFiles) {
    $rawContent = Get-Content -Path $file.FullName -Raw -Encoding UTF8
    # Entferne Block- und Zeilenkommentare für saubere Erkennung
    $cleanContent = [regex]::Replace($rawContent, '(?s)/\*.*?\*/', '')
    $cleanContent = [regex]::Replace($cleanContent, '(?m)//.*$', '')

    $matches = $typeRegex.Matches($cleanContent)
    $typeNames = @($matches | ForEach-Object { $_.Groups[1].Value } | Select-Object -Unique)

    if ($typeNames.Count -eq 0) {
        $violations.Add("1CPF-Verstoß in '$($file.FullName)': Keine Typ-Deklaration gefunden.")
    }
    elseif ($typeNames.Count -gt 1) {
        $violations.Add("1CPF-Verstoß in '$($file.FullName)': Mehrere Komponenten in einer Datei gefunden: $($typeNames -join ', '). Erlaubt ist exakt 1 Komponente pro Datei.")
    }
    else {
        $declaredType = $typeNames[0]
        $expectedBaseName = $file.BaseName
        # Für Code-Behind (.xaml.cs) z. B. MainWindow.xaml.cs -> MainWindow
        if ($expectedBaseName.EndsWith(".xaml")) {
            $expectedBaseName = $expectedBaseName.Substring(0, $expectedBaseName.Length - 5)
        }
        if ($declaredType -ne $expectedBaseName) {
            $violations.Add("1CPF-Namensverstoß in '$($file.Name)': Typ '$declaredType' stimmt nicht mit Dateiname '$expectedBaseName' überein.")
        }
    }
}

if ($violations.Count -gt 0) {
    Write-Host "`nArchitektur-Prüfung fehlgeschlagen ($($violations.Count) Verstöße):" -ForegroundColor Red
    foreach ($v in $violations) {
        Write-Host "  [X] $v" -ForegroundColor Red
    }
    exit 1
}

Write-Host "`n[OK] Architektur-Prüfung erfolgreich! ($($csFiles.Count) C#-Dateien geprüft, DDD & 1CPF eingehalten)." -ForegroundColor Green
exit 0

