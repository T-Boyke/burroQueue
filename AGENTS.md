# 🫏 burroQueue – Agent & Architecture Rules

Diese Regeln gelten **verbindlich** für alle KI-Agenten und Entwickler in diesem Repository.

## 1. Projekt-Kontext & Tech Stack

- **Produkt:** `burroQueue` – native Windows 11 Taskleisten-/Systray-App (kollaborative Office-Jukebox für YouTube-Links per Drag-and-Drop, Zwischenablage und lokaler Netzwerk-Synchronisation).
- **Sprache & Runtime:** C# 15 / .NET 11 (`net11.0` für Domain/Infrastructure, `net11.0-windows10.0.22621.0` für Presentation/Tests).
- **UI & Architektur:** Native WPF + `Wpf.Ui` (Fluent Design, Mica/Acrylic, Light/Dark Mode) + `CommunityToolkit.Mvvm` sowie Build-Time `UnoCSS 66.12.0` (0 Bootstrap) für das eingebettete LAN WebDrop-Frontend.
- **Tests:** `xUnit`, `FluentAssertions`, `NSubstitute`.
- **Lizenz:** GNU Affero General Public License v3.0 (`AGPL-3.0-only`).

## 2. Die 5 Eisernen Architektur- & Entwicklungs-Gesetze (Zwingend!)

### Gesetz 1: Domain-Driven Design (DDD) & Reinheit der Domain

- `src/burroQueue.Domain` kapselt die gesamte Kernlogik (`Playlist`, `Track`, `TrackId`, Domain Events, Domain Exceptions).
- `burroQueue.Domain` darf **null** externe NuGet-Pakete und **null** Projekt-Referenzen besitzen.
- Keine UI-Typen (`System.Windows.*`, `Wpf.Ui.*`), keine Netzwerk-/HTTP-Clients und keine Persistenz-Frameworks im Domain-Layer.

### Gesetz 2: Test-Driven Development (TDD – Red-Green-Refactor)

- **Keine Produktionscode-Änderung ohne fehlschlagenden Test vorab!**
- Jedes Feature und jeder Bugfix in `Domain`, `Infrastructure` und `ViewModels` folgt strikt dem Zyklus:
    1. **RED:** Schreibe einen präzisen xUnit-Test mit `FluentAssertions` in `src/burroQueue.Tests`.
    2. **GREEN:** Implementiere den minimal notwendigen Produktionscode, damit der Test besteht.
    3. **REFACTOR:** Räume den Code unter Beibehaltung grüner Tests gemäß SOLID auf.

### Gesetz 3: SFC / 1CPF (Single Component Per File)

- **Jede `.cs`- oder `.xaml`-Datei enthält exakt EINE Komponente** (1 `class`, 1 `interface`, 1 `struct`, 1 `record`, 1 `enum` oder 1 `delegate`).
- Der Dateiname muss **exakt** dem Typnamen entsprechen (z. B. `Playlist.cs` -> `public sealed class Playlist`).
- Verschachtelte oder mehrere öffentliche/interne Klassen in derselben Datei sind strengstens untersagt und brechen das CI-Gate (`./scripts/verify-architecture.ps1`).

### Gesetz 4: Performance First (< 30 MB RAM im Standby)

- Kein `WebView2`, kein `Electron`, keine unnötigen Hintergrund-Timer oder Polling-Loops.
- Vermeide unnötige Heap-Allokationen auf Hot-Paths (nutze `readonly record struct` für kleine Value Objects, `ValueTask` und Event-Driven I/O).
- Der UI-Thread darf niemals durch Netzwerk- oder YouTube-Metadaten-Aufrufe blockiert werden.

### Gesetz 5: Regelmäßige Atomic Commits & Conventional Commits

- Änderungen werden **kontinuierlich in kleinen, logisch abgeschlossenen Schritten** committet (nicht erst gebündelt am Ende).
- Jede Commit-Nachricht folgt strikt **Conventional Commits 1.0.0** (`feat(scope): ...`, `fix(scope): ...`, `test(scope): ...`, `refactor(scope): ...`, `ci(scope): ...`, `docs(scope): ...`, `chore(scope): ...`).
- Vor jedem Commit muss `./scripts/verify-architecture.ps1` bestanden sein. Details siehe `.agents/skills/github-atomic-commits/SKILL.md`.

## 3. Code-Stil & Namenskonventionen

- **Kommunikation & Dokumentation:** Deutsch.
- **Code-Bezeichner (Klassen, Methoden, Variablen, Tests):** Englisch.
- **Namespaces:** Immer File-Scoped (`namespace burroQueue.Domain.Model;`).
- **Nullability:** `<Nullable>enable</Nullable>` ist überall Pflicht; vermeide den Null-Forgiving-Operator (`!`), außer in streng begründeten Ausnahmefällen.
- **Validierung vor Abschluss einer Aufgabe:** Führe immer `./scripts/verify-architecture.ps1` und `dotnet test` (unter `.NET 11 SDK`) aus.
- **Repository-Gedächtnis & ADRs:** Halte den Projektstatus in `memory.md` und wesentliche Architekturentscheidungen in `decisions.md` aktuell (siehe `.agents/rules/memory-and-decisions.md`).
