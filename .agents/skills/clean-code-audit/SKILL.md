---
name: clean-code-audit
description: >-
  Executes a comprehensive Clean Code, SOLID, DDD boundary, and SFC/1CPF (Single Component Per File)
  audit across the burroQueue repository. Use this skill before committing, during code reviews, or
  when the user asks to check code quality and project rules.
---

# Clean Code & 1CPF Audit Skill

Dieser Skill prüft, ob alle Paradigmen aus der `README.md` und `AGENTS.md` eingehalten werden.

## Prüfschritte

### 1. Automatisiertes Architektur- & 1CPF-Gate ausführen
Führe immer zuerst das Repository-Prüfskript aus:
```powershell
pwsh -NoProfile -File ./scripts/verify-architecture.ps1
```
Das Skript kontrolliert:
- Hat `burroQueue.Domain.csproj` verbotene Paket- oder Projektreferenzen?
- Nutzen Dateien in `burroQueue.Domain` verbotene UI- oder Infrastruktur-Namespaces?
- Enthält jede `.cs`-Datei unter `src/` exakt **eine** Typ-Deklaration und stimmt der Dateiname mit dem Typnamen überein?

### 2. SOLID & Clean Code Checkliste
Prüfe geänderte Dateien manuell auf folgende Kriterien:
- **Single Responsibility Principle (SRP):** Hat jede Klasse genau einen Grund zur Änderung?
- **Kapselung:** Gibt es öffentliche mutable Listen (`List<T>`) im Domain-Modell? (Verboten -> stattdessen `IReadOnlyList<T>` und Domain-Methoden wie `Enqueue(...)` verwenden).
- **Methodenlänge:** Methoden sollten kompakt (in der Regel < 25 Zeilen) sein und maximal eine Abstraktionsebene umfassen.
- **Dependency Inversion:** Hängt `Presentation` oder `Application`-Logik von Interfaces (`IYouTubeUrlParser`, `IQueueSyncListener`) statt von konkreten Infrastruktur-Klassen ab?

### 3. Compiler- & Format-Check (wenn .NET SDK installiert)
```powershell
dotnet format burroQueue.sln --verify-no-changes
dotnet build burroQueue.sln --configuration Release -warnaserror
dotnet test burroQueue.sln --configuration Release
```

