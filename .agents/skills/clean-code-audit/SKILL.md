---
name: clean-code-audit
description: >-
  Executes a comprehensive Clean Code, SOLID, DDD boundary, SFC/1CPF (Single Component Per File),
  and .NET 11 analyzer audit across the burroQueue repository. Use this skill before committing,
  during code reviews, or when the user asks to check code quality and project rules.
---

# Clean Code, `.NET 11` & 1CPF Audit Skill

Dieser Skill prüft, ob alle Architektur- und Qualitätsgesetze aus `AGENTS.md` und `.editorconfig` eingehalten werden.

## Prüfschritte

### 1. Automatisiertes Architektur- & 1CPF-Gate ausführen
Führe immer zuerst das Repository-Prüfskript aus:
```powershell
pwsh -NoProfile -File ./scripts/verify-architecture.ps1
```
Das Skript kontrolliert:
- Besitzt `burroQueue.Domain.csproj` exakt **0** NuGet-Paket- und **0** Projektreferenzen?
- Nutzen Dateien in `burroQueue.Domain` verbotene UI- oder Infrastruktur-Namespaces?
- Enthält jede `.cs`-Datei unter `src/` exakt **eine** Typ-Deklaration und stimmt der Dateiname exakt mit dem Typnamen überein?

### 2. SOLID, Clean Code & `.NET 11` Analyzer-Guardrails
Prüfe geänderte Dateien vor dem Build auf folgende häufige Fehlerquellen:
- **`IDE1006` Naming Rule (`_camelCase`):** `private` und `private static readonly` Felder müssen mit Unterstrich beginnen (z. B. `private static readonly string _cachedCss`, **nicht** `CachedCss`). `dotnet format` kann `IDE1006` nicht automatisch korrigieren!
- **`CA2027` Unawaited Task in `Task.WhenAny`:** Niemals ein unawaited `Task.Delay(...)` direkt in `Task.WhenAny(task, Task.Delay(...))` übergeben – unter `.NET 11` mit `-warnaserror` bricht dies den Build. Stattdessen `await task.WaitAsync(TimeSpan.FromSeconds(...))` oder einen `CancellationTokenSource` zum Abbrechen von `Task.Delay` nutzen.
- **Kapselung & Performance (`< 30 MB` RAM):** Keine öffentlichen mutablen Listen (`List<T>`) im Domain-Modell (`IReadOnlyList<T>` verwenden); kleine Value Objects als `readonly record struct` definieren; `ArrayPool<byte>.Shared` für temporäre Medien-/Bildpuffer nutzen.
- **Final Newline & Markdown:** Jede Datei endet mit genau einem Zeilenumbruch (`insert_final_newline = true`).
- **GitHub Actions `.NET 11` RC SDK:** Solange `.NET 11` im RC-Channel ist, muss `actions/setup-dotnet@v6` mit `dotnet-version: '11.0.x'` und `dotnet-quality: 'preview'` konfiguriert bleiben (da `global-json-file` ohne vollständiges Build-Suffix HTTP 404 liefert).

### 3. Compiler-, Format- & TDD-Check (mit Dev-Drive Caches auf `D:\`)
```powershell
$env:NUGET_PACKAGES = "D:\packages\nuget"
$env:npm_config_cache = "D:\packages\npm-cache"

dotnet format burroQueue.sln --no-restore --verify-no-changes
dotnet build burroQueue.sln --configuration Release --no-restore -warnaserror
dotnet test burroQueue.sln --configuration Release --no-build
```
