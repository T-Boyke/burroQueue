---
name: tdd-ddd-workflow
description: >-
  Enforces the strict Test-Driven Development (Red-Green-Refactor) and Domain-Driven Design
  workflow for burroQueue. Use this skill whenever creating a new feature, adding a domain entity,
  value object, domain service, infrastructure adapter, or fixing a bug in the C# codebase.
---

# TDD & DDD Workflow Skill (`burroQueue`)

Dieser Skill definiert den verbindlichen Entwicklungsablauf für jede Code-Änderung in `burroQueue`.

## Wann dieser Skill genutzt wird
- Hinzufügen oder Ändern von Domain-Logik (`Playlist`, `Track`, Queue-Ordering, Voting, Limits).
- Implementieren von Infrastructure-Services oder ViewModels.
- Beheben von Bugs durch Reproduktion per Unit-Test.

## Schritt-für-Schritt Vorgehen

### Phase 1: Domain-Design & 1CPF-Planung
1. Prüfe, ob die Anforderung in `burroQueue.Domain` gehört (reine Geschäftsregel ohne I/O).
2. Bestimme die benötigten Typen und plane **für jeden Typ eine eigene Datei** (1CPF / SFC):
   - Value Objects als `public readonly record struct` oder `public sealed record` in `src/burroQueue.Domain/Model/`.
   - Entities / Aggregate Roots als `public sealed class` mit gekapseltem Zustand (z. B. `IReadOnlyList<Track>` statt öffentlicher `List<Track>`).
   - Domain Exceptions in `src/burroQueue.Domain/Exceptions/`.

### Phase 2: RED – Fehlschlagenden Test zuerst schreiben
1. Erstelle oder öffne die passende Testdatei unter `src/burroQueue.Tests/<Layer>/<ClassName>Tests.cs`.
2. Benenne Testmethoden nach dem Schema: `MethodOrScenario_WhenCondition_ShouldExpectedBehavior`.
3. Verwende `xUnit` (`[Fact]`, `[Theory]`) und `FluentAssertions` (`.Should().Be(...)`, `.Should().Throw<...>()`).
4. Führe `dotnet test` aus und verifiziere, dass der Test fehlschlägt (oder noch nicht kompiliert).

### Phase 3: GREEN – Minimale Implementierung
1. Erstelle die Produktionsdatei exakt mit dem Namen des Typs (1CPF).
2. Verwende File-Scoped Namespaces (`namespace burroQueue.Domain.Model;`).
3. Schreibe nur so viel Code, wie nötig ist, um den Test auf Grün zu bringen.
4. Führe `dotnet test` erneut aus.

### Phase 4: REFACTOR & Architektur-Verifikation
1. Optimiere Lesbarkeit, Unveränderlichkeit (Immutability) und Speichereffizienz.
2. Führe das Architektur-Prüfskript aus:
   ```powershell
   ./scripts/verify-architecture.ps1
   ```
3. Stelle sicher, dass 0 Compiler-Warnungen und 0 Architektur-Verstöße vorliegen.

