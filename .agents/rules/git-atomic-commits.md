# Git Workflow & Atomic Commits Regeln

## 1. Verpflichtung zu regelmäßigen, atomaren Commits
- Änderungen dürfen **nicht** in riesigen „Megacommit“-Batches gesammelt werden.
- Commits erfolgen in kleinen, abgeschlossenen Schritten, idealerweise entlang des TDD-Zyklus:
  - Commit 1: `test(...)`: Neuer fehlschlagender Test
  - Commit 2: `feat(...)`: Minimale Implementierung zur Erfüllung des Tests
  - Commit 3: `refactor(...)`: Bereinigung / Optimierung (sofern nötig)

## 2. Conventional Commits 1.0.0
Jede Commit-Nachricht **muss** dem Standard `type(scope): description` entsprechen:
- Erlaubte Typen: `feat`, `fix`, `test`, `refactor`, `perf`, `ci`, `docs`, `chore`.
- Scopes: `domain`, `infra`, `presentation`, `tests`, `ci`, `repo`.
- Sprache der Commit-Message: Englisch, im Imperativ / Präsens (`add feature` statt `added feature`).

## 3. Hygiene vor dem Commit
- Vor jedem Commit muss `./scripts/verify-architecture.ps1` erfolgreich sein.
- Keine temporären Dateien, Runner-Ordner oder ungetrackten Build-Artefakte stagen.
- Kein Zerstören der Build-Stabilität (`broken build` ist auf `main` unzulässig).

