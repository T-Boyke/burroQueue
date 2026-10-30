---
name: github-atomic-commits
description: >-
  Enforces SSH-Verified Conventional Commits, regular atomic Git commits, 1:1 Commit-to-Issue
  Kanban Project #7 synchronization, and PowerShell/gh GraphQL hygiene in burroQueue. Use this
  skill whenever planning commits, staging changes, pushing to GitHub, or updating issues and
  Kanban cards.
---

# GitHub, Verified Atomic Commits & 1:1 Kanban Sync Skill (`burroQueue`)

Dieser Skill definiert den verbindlichen Git- und GitHub-Governance-Workflow für alle Änderungen in `burroQueue`.

---

## 1. Das Prinzip der atomaren, SSH-signierten Commits

Ein atomarer Commit umfasst **genau eine logische Änderung** (Single Logical Change).

### Qualitätskriterien vor JEDEM Commit
1. `./scripts/verify-architecture.ps1` muss erfolgreich durchlaufen (`[OK]`).
2. `dotnet format burroQueue.sln --no-restore --verify-no-changes` muss ohne `IDE1006`- oder Formatfehler bestehen.
3. Keine unfertigen temporären Dateien oder Build-Artefakte stagen (`git status` prüfen).
4. **Verpflichtendes SSH Commit-Signing (`Verified` auf GitHub):**
   - Jeder Commit und jeder Release-Tag **muss** kryptografisch signiert sein (`commit.gpgsign=true`, `tag.gpgsign=true`, `gpg.format=ssh`, Identität `37149164+T-Boyke@users.noreply.github.com`).
   - Der Parameter `--no-gpg-sign` ist strengstens untersagt!
   - Prüfe per `git log -1 --show-signature`, dass `Good "git" signature` vorliegt.

---

## 2. Conventional Commits 1.0.0 Spezifikation

```text
<type>(<scope>): <kurze beschreibung im präsens> (Closes #<issue>)
```

- **Typen:** `feat`, `fix`, `test`, `refactor`, `perf`, `style`, `ci`, `docs`, `chore`.
- **Scopes:** `domain`, `infra`, `presentation` (oder `ui`), `webdrop`, `audio`, `telephony`, `network`, `tests`, `ci`, `scripts`, `agents`, `repo`.

---

## 3. Die 5 Entwicklungs-Säulen & Automatischer 1:1 Commit-zu-Issue Kanban-Sync

Jedes Feature-/Bug-Issue und **jeder einzelne Git-Commit (1:1 `commit-record`)** wird im GitHub Project v2 **[Project #7](https://github.com/users/T-Boyke/projects/7)** (`PVT_kwHOAjbZ7M4BpeGG`) mit `Assignee: T-Boyke`, einem Milestone und als `Sub-Issue` unter einem der **5 Säulen-Epics** geführt:

1. **`pillar:1-core-domain` (`Epic #23`)** – Core Domain, Queue Engine & TOML Persistence
2. **`pillar:2-audio-hardware` (`Epic #24`)** – Audio Engine, VoIP Auto-Ducking & Hardware/AI
3. **`pillar:3-network-webdrop` (`Epic #25`)** – P2P LAN Sync, YouTube & UnoCSS 66.12 WebDrop
4. **`pillar:4-fluent-ux` (`Epic #26`)** – Native Windows 11 Fluent UI, Desktop UX & i18n
5. **`pillar:5-devsecops-release` (`Epic #27`)** – DevSecOps, Code Signing, Verified Git & `.NET 11` CI/CD

### Pflichtschritt nach jedem `git push`
Führe unmittelbar nach `git push origin main` das begleitende Sync-Skript [sync-commit-issues.ps1](./scripts/sync-commit-issues.ps1) aus (optional mit `-CompletedIssues` für frisch geschlossene Issues):

```powershell
# Synchronisiert alle neuen Commits 1:1 als Issues + verschiebt abgeschlossene Issues auf 'Done'
pwsh -NoProfile -File ./.agents/skills/github-atomic-commits/scripts/sync-commit-issues.ps1 -Milestone "v1.1.0" -CompletedIssues 6,17
```

---

## 4. Windows PowerShell & `gh` CLI GraphQL Best Practices

Bei direkten `gh api` / GraphQL-Aufrufen unter Windows PowerShell gelten folgende Regeln, um Parsing- und Encoding-Fehler zu vermeiden:

1. **UTF-8 ohne BOM & `--input` verwenden:** Schreibe JSON-Bodies immer per `[System.IO.File]::WriteAllText($tmpFile, $json, [System.Text.UTF8Encoding]::new($false))` und übergib sie mit `gh api ... --input $tmpFile` (verhindert PowerShell-5.1-BOM-Fehler und CLI-Quote-Stripping bei `-f` oder `--jq`).
2. **Single-Quoted Here-Strings (`@'...'@`) für Markdown:** Nutze für Issue-/PR-Texte mit Backticks niemals doppelte Anführungszeichen (`@"..."@`), da PowerShell 7 Sequenzen wie `` `unocss` `` als ungültigen `` `u `` Unicode-Escape interpretiert. Nutze immer `@'...'@` mit `.Replace("__KEY__", $val)`.
3. **GraphQL Preview-Header & bestehende Project-Items:**
   - Für `addSubIssue` und `addBlockedBy` immer `-H "GraphQL-Features: sub_issues,issue_dependencies"` übergeben.
   - Vor `addProjectV2ItemById` bei bereits existierenden Issues zuerst `node(id: $id) { ... on Issue { projectItems(first: 10) { nodes { id project { id } } } } }` abfragen, um `Content already exists in this project` (`UNPROCESSABLE`) zu vermeiden.
