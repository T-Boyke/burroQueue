# Git Workflow, Verified Commits & 1:1 Kanban Governance

## 1. Verpflichtung zu regelmäßigen, atomaren Commits
- Änderungen dürfen **nicht** in riesigen „Megacommit“-Batches gesammelt werden.
- Commits erfolgen in kleinen, abgeschlossenen Schritten entlang des TDD-Zyklus:
  - Commit 1: `test(...)`: Neuer fehlschlagender Test (oder atomarer TDD-Feature-Schritt mit Test + minimaler Implementierung)
  - Commit 2: `feat(...)` / `fix(...)`: Minimale Implementierung zur Erfüllung des Tests
  - Commit 3: `refactor(...)`: Bereinigung / Optimierung unter grünen Tests

## 2. Conventional Commits 1.0.0
Jede Commit-Nachricht **muss** dem Standard `type(scope): description` entsprechen:
- Erlaubte Typen: `feat`, `fix`, `test`, `refactor`, `perf`, `style`, `ci`, `docs`, `chore`.
- Scopes: `domain`, `infra`, `presentation`, `webdrop`, `audio`, `telephony`, `network`, `tests`, `ci`, `agents`, `repo`.
- Sprache der Commit-Message: Englisch, im Imperativ / Präsens (`add feature` statt `added feature`).

## 3. Hygiene, SSH-Signatur (`Verified`) & 1:1 Kanban-Sync
- Vor jedem Commit muss `./scripts/verify-architecture.ps1` erfolgreich sein.
- Keine temporären Dateien, Runner-Ordner oder ungetrackten Build-Artefakte stagen; abschließende Zeilenumbrüche (`insert_final_newline = true`) beachten.
- Kein Zerstören der Build-Stabilität (`broken build` ist auf `main` unzulässig).
- **SSH Verified Pflicht:** Jeder Commit und jeder Release-Tag **muss** mit dem konfigurierten Ed25519 SSH-Key kryptografisch signiert werden (`git commit -S`, `commit.gpgsign = true`, `tag.gpgsign = true`; `--no-gpg-sign` ist strengstens verboten).
- **1:1 GitHub Kanban-Sync:** Nach jedem `git push` muss für neue Commits das 1:1 `commit-record`-Issue (`Assignee: T-Boyke`, Säulen-Epic `#23`–`#27`, Project `#7` Status `Done`) über `pwsh -NoProfile -File ./.agents/skills/github-atomic-commits/scripts/sync-commit-issues.ps1` synchronisiert werden.
