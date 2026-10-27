# Pull Request

## Zusammenfassung der Änderungen

<!-- Bitte beschreibe kurz und präzise, was geändert wurde und warum. -->

## Betroffene Schichten

- [ ] `burroQueue.Domain` (Reine Geschäftslogik, 0 Dependencies)
- [ ] `burroQueue.Infrastructure` (Audio, YouTube, Network, Persistence, Telephony)
- [ ] `burroQueue.Presentation` (WPF UI, ViewModels, Systray)
- [ ] `burroQueue.Tests` (Unit, Integration, System, E2E)
- [ ] Dokumentation & Build-Skripte

## Checkliste vor dem Einreichen

- [ ] **Architektur-Prüfung:** `./scripts/verify-architecture.ps1` ausgeführt und erfolgreich bestanden (0 Fehler).
- [ ] **1CPF:** Jede `.cs`-Datei enthält exakt eine Komponente, Dateiname entspricht Typname.
- [ ] **TDD:** Jeder Produktionsänderung liegt ein fehlschlagender xUnit-Test zugrunde.
- [ ] **Tests & Build:** `dotnet test` läuft fehlerfrei durch (0 Warnungen bei `-warnaserror`).
- [ ] **Code Formatting:** `dotnet format --verify-no-changes` meckert nicht.
- [ ] **Conventional Commits:** Alle Commit-Nachrichten folgen `feat:`, `fix:`, `refactor:`, `test:`, `docs:`, etc.
- [ ] **Memory & ADR:** Bei architektonischen Änderungen wurden `memory.md` und `decisions.md` gepflegt.
