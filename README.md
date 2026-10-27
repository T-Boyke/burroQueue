<p align="center">
  <img src="assets/logo.png" width="140" alt="burroQueue Logo" />
</p>

<h1 align="center">burroQueue</h1>

<p align="center">
  <b>Kollaborative Windows 11 Office-Jukebox</b><br />
  Mit VoIP Auto-Ducking (SwyxIt!), P2P-LAN-Discovery, YouTube-Metadaten und Web-Drop für das Büro.
</p>

<p align="center">
  <img src="https://img.shields.io/badge/Platform-Windows%2011-0078D4?style=flat-square" alt="Platform" />
  <img src="https://img.shields.io/badge/Runtime-.NET%2010%20%7C%20C%23%2014-512BD4?style=flat-square" alt="Runtime" />
  <img src="https://img.shields.io/badge/UI-WPF%20%2B%20Wpf.Ui%20(Mica)-005FB8?style=flat-square" alt="UI" />
  <img src="https://img.shields.io/badge/License-AGPL--3.0-blue.svg?style=flat-square" alt="License" />
</p>

---

## Kernfunktionen

- **VoIP Auto-Ducking (SwyxIt! 11 & Windows Core Audio):** Überwacht eingehende und aktive Anrufe. Die Musiklautstärke senkt sich bei Anrufbeginn automatisch ab und fadet nach Gesprächsende wieder hoch.
- **Getrenntes Audio-Routing:** Jukebox-Sound läuft dediziert auf der Büro-Box (Bluetooth/Klinke), während Telefonate ungestört auf dem Headset bleiben.
- **P2P-LAN Synchronisation:** Instanzen finden sich im Firmennetzwerk via UDP-Multicast (`239.255.42.99:41234`). Gruppenmitglieder können die Gruppe umbenennen und die Wiedergabe steuern.
- **Gemeinsame YouTube-Playlists:** Unterstützung für Einladungslinks (`&jct=...`), die neuen Gruppenmitgliedern automatisch bereitgestellt werden.
- **Mobile Web-Drop (`http://<PC-IP>:8080`):** Leichtgewichtiges Browser-Interface für Kollegen auf Smartphones, Macs oder Linux-Rechnern ohne App-Installation (< 1 MB RAM).
- **Interaktive Warteschlange & Vote-to-Skip:** Warteschlange einsehen, einzelne Tracks entfernen oder bei unpassenden Songs demokratisch für Skip abstimmen.
- **Ressourceneffizienz:** Standby-Speicherbedarf unter 30 MB RAM durch native WPF-Architektur ohne WebView2- oder Electron-Wrapper.

---

## Architektur & Richtlinien

Das Projekt folgt einer strikten 4-Schichten-Architektur:

1. **`src/burroQueue.Domain` (Reine Geschäftslogik):**
   Kapselt Playlists, Tracks, Gruppen und Abstimmungen. Frei von NuGet-Paketen, UI- oder Netzwerk-Referenzen.
2. **`src/burroQueue.Infrastructure` (Adapter & I/O):**
   Telefonie-Anbindung (`CLMgr.ClientLineMgr`), P2P-Netzwerk-Sockets, YouTube oEmbed-Metadaten, Audiosteuerung und lokaler Web-Server.
3. **`src/burroQueue.Presentation` (Native Windows 11 UI):**
   Mica Fluent Window, MVVM via `CommunityToolkit.Mvvm`, System Theme Watcher und Systray-Hintergrundbetrieb.
4. **`src/burroQueue.Tests` (Test-Suite):**
   Unit- und Integrationstests mit `xUnit`, `FluentAssertions` und `NSubstitute`.

### Qualitäts-Gate (SFC / 1CPF & DDD)
Jede C#-Datei enthält exakt eine Komponente (Single Component Per File). Das Architektur-Gate prüft vor Commits und in der CI:
```powershell
./scripts/verify-architecture.ps1
```

---

## Schnellstart

### Voraussetzungen
- Windows 11 (Version 22H2 oder neuer empfohlen)
- .NET 10 SDK

### Bauen und Testen
```bash
# Repository klonen
git clone https://github.com/T-Boyke/burroQueue.git
cd burroQueue

# Architektur-Konformität prüfen
./scripts/verify-architecture.ps1

# Solution bauen & Tests ausführen
dotnet build burroQueue.sln
dotnet test burroQueue.sln
```

### Starten
```bash
dotnet run --project src/burroQueue.Presentation/burroQueue.Presentation.csproj
```

---

## CI/CD & Lokaler Runner

- **Cloud CI:** [`.github/workflows/ci.yml`](.github/workflows/ci.yml) validiert Architektur, Code-Formatierung und Tests auf `windows-latest`.
- **Release:** [`.github/workflows/release.yml`](.github/workflows/release.yml) packt bei Git-Tags ein Single-File Release für Windows 11 x64.
- **Self-Hosted Runner:** Lokales Setup-Skript unter [`scripts/runner/setup-runner.ps1`](scripts/runner/setup-runner.ps1) für Entwicklungsrechner.

---

## Lizenz

Dieses Projekt ist lizenziert unter der **GNU Affero General Public License v3.0 (AGPL-3.0)**. Siehe [LICENSE](LICENSE) für den vollständigen Lizenztext.
