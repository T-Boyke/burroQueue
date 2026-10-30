---
name: github-atomic-commits
description: >-
  Enforces Conventional Commits, regular atomic Git commits, and GitHub pull request / branch
  hygiene in burroQueue. Use this skill whenever planning commits, staging changes, authoring
  commit messages, or creating branches and pull requests.
---

# GitHub & Atomic Commits Skill (`burroQueue`)

Dieser Skill definiert den verbindlichen Git- und GitHub-Workflow für alle Änderungen in `burroQueue`.

---

## 1. Das Prinzip der atomaren Commits (Atomic Commits)

Ein atomarer Commit umfasst **genau eine logische Änderung** (Single Logical Change).

### Wann wird committed?
- Nicht erst am Ende eines großen Features!
- Committe **nach jedem erfolgreichen Teilschritt** (z. B. nach Abschluss einer TDD-Phase: Red -> Green -> Refactor).
- **Faustregel:** Wenn die Commit-Nachricht ein „und“ / „and“ benötigt, um zwei verschiedene Dinge zu beschreiben, teile den Commit in zwei auf!

### Qualitätskriterien vor JEDEM Commit:
1. `./scripts/verify-architecture.ps1` muss erfolgreich durchlaufen (`[OK]`).
2. Keine unfertigen temporären Dateien oder Build-Artefakte stagen (`git status` prüfen).
3. Jeder Commit muss das Repository in einem konsistenten, kompilierbaren Zustand hinterlassen.
4. **Verpflichtendes Commit-Signing (`Verified` auf GitHub):**
   - Jeder Commit und jeder Release-Tag **muss** kryptografisch signiert sein (`commit.gpgsign=true`, `tag.gpgsign=true`, `gpg.format=ssh`).
   - Der Parameter `--no-gpg-sign` ist strengstens untersagt!
   - Prüfe im Zweifel per `git log -1 --show-signature`, dass eine gültige Signatur vorliegt.

---

## 2. Conventional Commits 1.0.0 Spezifikation

Jeder Commit-Header folgt strikt dem Schema:
```text
<type>(<scope>): <kurze beschreibung im präsens>

[optionaler längerer body]

[optionale footer, z. B. Closes #123]
```

### Gültige Typen (`<type>`):
| Typ | Verwendung | Beispiel |
| :--- | :--- | :--- |
| `feat` | Ein neues sichtbares Feature oder Verhalten | `feat(domain): add queue size limit to playlist` |
| `test` | Hinzufügen oder Anpassen von Tests (TDD RED Phase) | `test(domain): verify playlist capacity limits` |
| `fix` | Bugfix im Produktivcode | `fix(infra): handle youtube timestamp queries correctly` |
| `refactor` | Codeänderung ohne Verhaltens- oder Teständerung | `refactor(presentation): extract drop zone styling to resource` |
| `ci` | Änderungen an GitHub Actions oder lokalen Runnern | `ci(github): add matrix build for windows runners` |
| `docs` | Dokumentation, README, Markdown-Updates | `docs(readme): add local runner setup guide` |
| `chore` | Build-Skripte, Tooling, .editorconfig, Dependencies | `chore(repo): update directory build props for c# 14` |

### Zulässige Scopes (`<scope>`):
- `domain`: `src/burroQueue.Domain`
- `infra`: `src/burroQueue.Infrastructure`
- `presentation` oder `ui`: `src/burroQueue.Presentation`
- `tests`: `src/burroQueue.Tests`
- `ci`: `.github/workflows` oder `scripts/runner`
- `scripts`: Skripte im Root wie `verify-architecture.ps1`
- `repo`: Übergreifende Repository-Konfigurationen

---

## 3. GitHub Branch & PR Workflow

1. **Feature Branches:**
   - Schema: `feature/<kurzbeschreibung>` (z. B. `feature/systray-drop-target`)
   - Bugfixes: `fix/<kurzbeschreibung>` (z. B. `fix/url-query-parameter`)
   - Chores / CI: `chore/<kurzbeschreibung>`

2. **Commit-Schritte via CLI:**
   ```powershell
   # 1. Status prüfen & gezielt stagen (niemals blind git add . wenn noch Unfertiges herumliegt)
   git status
   git add <gezielte-dateien>

   # 2. Architektur-Gate prüfen
   ./scripts/verify-architecture.ps1

   # 3. Atomaren, SSH-signierten Commit mit Conventional Commit Message erstellen
   git commit -S -m "feat(domain): add playlist enqueue validation"
   ```

---

## 4. GitHub Governance & Die 5 Entwicklungs-Säulen (`Projects v2 Kanban`)

Jedes Issue und jeder dokumentierte Commit wird im GitHub Project (`https://github.com/users/T-Boyke/projects/7`) mit Assignee `T-Boyke`, einem Milestone und exakt einer der **5 Entwicklungs-Säulen** geführt:

1. **`pillar:1-core-domain`** – Core Domain, Queue Engine & TOML Persistence
2. **`pillar:2-audio-hardware`** – Audio Engine, VoIP Auto-Ducking & Hardware/AI
3. **`pillar:3-network-webdrop`** – P2P LAN Sync, YouTube & UnoCSS 66.12 WebDrop
4. **`pillar:4-fluent-ux`** – Native Windows 11 Fluent UI, Desktop UX & i18n
5. **`pillar:5-devsecops-release`** – DevSecOps, Code Signing, Verified Git & `.NET 11` CI/CD


