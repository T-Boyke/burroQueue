# Sicherheitsrichtlinie & Code-Signing-Architektur für burroQueue

## Unterstützte Versionen

| Version | Runtime / Stack | Unterstützt |
| :--- | :--- | :--- |
| `2.0.x` | `.NET 11` / `C# 15` / `UnoCSS 66.12.0` | :white_check_mark: |
| `1.0.x` | `.NET 10` / `C# 14` | :white_check_mark: (Sicherheitsfixes) |
| `< 1.0` | Vorabversionen | :x: |

---

## 1. Windows Defender SmartScreen & Kostenloses OSS Code-Signing (`SignPath.io`)

### Warum greift der Windows-Schutz ohne Zertifikat?
Windows 11 prüft jede heruntergeladene ausführbare Datei (`burroQueue-Setup-x64.exe` sowie die darin enthaltene `burroQueue.exe`) mittels **Microsoft Defender SmartScreen** auf eine gültige **Authenticode-Signatur** (X.509 Code-Signing-Zertifikat einer vertrauenswürdigen Root-CA mit RFC-3161 Zeitstempel). Fehlt diese Signatur oder wird lediglich ein selbstsigniertes Zertifikat verwendet, blockiert Windows die Installation mit der Warnung *„Der Computer wurde durch Windows geschützt“*.

### Unsere Lösung: Zweistufige Signatur über SignPath Foundation (Kostenlos für AGPL-3.0)
Als quelloffenes Projekt unter **GNU AGPL-3.0** nutzt `burroQueue` die kostenlose Open-Source-Infrastruktur von **[SignPath.io](https://about.signpath.io/)** mit einem öffentlichen Zertifikat der **SignPath Foundation**:

1. **Stufe 1 – Innere Anwendungs-Binärdatei (`burroQueue.exe`):** Nach `dotnet publish` lädt [`.github/workflows/release.yml`](.github/workflows/release.yml) die unsignierte `burroQueue.exe` als attestiertes GitHub-Actions-Artefakt hoch, lässt sie durch `SignPath/github-action-submit-signing-request@v2` kryptografisch signieren und prüft das Ergebnis via `Get-AuthenticodeSignature`.
2. **Stufe 2 – Äußerer Inno-Setup-Installer (`burroQueue-Setup-x64.exe`):** Erst danach verpackt Inno Setup die bereits signierte `burroQueue.exe` in den Installer. Anschließend wird auch `burroQueue-Setup-x64.exe` über SignPath.io signiert, per `Get-AuthenticodeSignature` verifiziert und zusammen mit `SHA256SUMS.txt` veröffentlicht.

> **Code Signing Policy & Attribution:**
> Kostenloses Code-Signing wird bereitgestellt durch [SignPath.io](https://about.signpath.io/), Zertifikat durch die [SignPath Foundation](https://signpath.org/).
> - **Committers & Approvers:** `@T-Boyke` (Maintainer)
> - **Datenschutzrichtlinie:** `burroQueue` überträgt keinerlei Telemetrie-, Tracking- oder personenbezogene Daten an externe Server; sämtliche Queue-Synchronisation erfolgt lokal im LAN (UDP-Multicast) bzw. direkt gegen vom Nutzer konfigurierte YouTube-Links.

---

## 2. Einsatz von GitHub Actions Secrets & Variables

**Architektur-Regel:**
- **JA – für CI/CD Release-Pipelines:** Kryptografische API-Tokens für Code-Signing und Paket-Veröffentlichungen (z. B. WinGet) werden **ausschließlich** in **GitHub Actions Secrets** gespeichert und niemals im Quellcode, in `.toml`-Dateien oder in Skripten abgelegt.
- **NEIN – niemals in Client-Binaries:** In `burroQueue.exe` selbst dürfen **keine** Secrets oder privaten API-Keys eingebettet werden, da Desktop-Anwendungen grundsätzlich dekompilierbar sind und unter AGPL-3.0 reproduzierbar aus dem öffentlichen Quellcode gebaut werden müssen.

### Erforderliche GitHub Secrets & Repository Variables (`Settings → Secrets and variables → Actions`)

| Typ | Name | Beschreibung / Standardwert |
| :--- | :--- | :--- |
| **Secret** | `SIGNPATH_API_TOKEN` | Geheimer CI-Connector API-Token aus SignPath.io (niemals öffentlich loggen). |
| **Variable** | `SIGNPATH_ORGANIZATION_ID` | Organisations-UUID aus dem SignPath.io Dashboard. |
| **Variable** | `SIGNPATH_PROJECT_SLUG` | Projekt-Slug in SignPath (Standard: `burroqueue`). |
| **Variable** | `SIGNPATH_SIGNING_POLICY_SLUG` | Signing-Policy-Slug in SignPath (Standard: `release-signing`). |
| **Variable** *(optional)* | `SIGNPATH_EXE_ARTIFACT_CONFIG_SLUG` | Optionale Artifact-Configuration für `burroQueue.exe`. |
| **Variable** *(optional)* | `SIGNPATH_INSTALLER_ARTIFACT_CONFIG_SLUG` | Optionale Artifact-Configuration für `burroQueue-Setup-x64.exe`. |
| **Secret** *(optional)* | `WINGET_GITHUB_TOKEN` | Fine-Grained PAT für automatische WinGet-Manifest-PRs (`microsoft/winget-pkgs`). |

### Einmalige Aktivierung in 4 Schritten
1. Unter [signpath.org/projects](https://signpath.org/) das kostenlose Open-Source-Projekt `T-Boyke/burroQueue` (`AGPL-3.0`) anmelden und mit GitHub verknüpfen.
2. Im SignPath-Projekt den **GitHub Actions Trusted Build Connector** aktivieren und einen **CI User API Token** erzeugen.
3. In GitHub unter `https://github.com/T-Boyke/burroQueue/settings/secrets/actions` das Secret `SIGNPATH_API_TOKEN` sowie die Variable `SIGNPATH_ORGANIZATION_ID` hinterlegen.
4. Sobald beide Werte gesetzt sind, erkennt [`.github/workflows/release.yml`](.github/workflows/release.yml) dies automatisch (`SIGNPATH_ENABLED == 'true'`) und signiert jeden Release-Tag `v*.*.*` ohne weitere Workflow-Änderung.

---

## 3. Verifizierte Git-Commits (`SSH Ed25519 Verified`)

Jeder Commit und jeder Release-Tag auf `main` wird lokal mit einem **Ed25519 SSH Signing Key** kryptografisch signiert (`gpg.format = ssh`, `commit.gpgsign = true`, `tag.gpgsign = true`) und gegen die verifizierte GitHub-Identität von `@T-Boyke` geprüft (**`Verified`**-Badge auf GitHub). Unsignierte Commits (`--no-gpg-sign`) sind gemäß Projektregeln untersagt.

---

## 4. Sicherheitslücken melden

Wenn du eine Sicherheitslücke in `burroQueue` entdeckst:

1. Öffne bitte **kein** öffentliches Issue im GitHub-Repository.
2. Melde die Schwachstelle vertraulich über [GitHub Private Vulnerability Reporting](https://github.com/T-Boyke/burroQueue/security/advisories/new) an `@T-Boyke`.
3. Bitte beschreibe:
   - Art der Schwachstelle (z. B. Denial-of-Service via UDP-Multicast, Input-Parsing via URL Drop)
   - Schritte zur Reproduktion (Proof-of-Concept)
   - Mögliche Auswirkungen auf das lokale Büronetzwerk

Wir prüfen jede Meldung unverzüglich und stellen schnellstmöglich einen signierten Patch bereit.
