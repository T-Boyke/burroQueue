# 🫏 burroQueue – Repository Memory

Letzte Aktualisierung: 2026-10-30 | Ziel: Version 2.0 (.NET 11, UnoCSS 66.12.0 & SignPath Code Signing)

---

## 1. Projekt-Status & Kernmetriken

- **Version:** `1.0.0` (Produktionsreif) → `2.0.0` Tech-Stack & Governance Upgrade aktiv
- **Zielsystem:** Windows 11 (Mica, Fluent Design, Systray-Jukebox)
- **Runtime:** `.NET 11` / `C# 15` (`net11.0` für Domain/Infrastructure, `net11.0-windows10.0.22621.0` für Presentation/Tests)
- **WebDrop Frontend:** Build-Time `UnoCSS 66.12.0` (`src/burroQueue.WebDrop`, Zero Bootstrap, `< 9 KB` Embedded Resource `webdrop.uno.min.css` via `WebDropStylesheetProvider`, 0 Runtime-Overhead)
- **Architektur:** Reine Domain-Driven Design Schichten (Domain = 0 externe Dependencies, Single Component Per File)
- **Aktuelle Testanzahl:** 475 Tests (100% grün auf `net11.0`)
- **Aktuelle Code-Coverage:**
  - `burroQueue.Domain`: 100,00% Line Coverage
  - `burroQueue.Presentation`: 100,00% Line Coverage
  - `burroQueue.Infrastructure`: 99,3% Line Coverage
  - **Gesamtlösung:** > 99,7% Line Coverage
- **STARFACE VoIP & Ducking Release Delay:** `StarfaceTelephonyProvider` mit 3-stufiger Anruferkennung (WCF Named Pipe `net.pipe://localhost/StarfaceUcClient/UccService`, inkrementeller Log-Tail auf `UCC*.log` und CoreAudio Session Monitoring). Konfigurierbare Nachlaufzeit nach Anrufende (`DuckingReleaseDelaySeconds`, Standard 3s), bevor `MasterVolume` wiederhergestellt wird.
- **Bidirektionaler YouTube-Playlist-Sync:** `YouTubePlaylistWriter` schreibt neu eingereihte Tracks dauerhaft per InnerTube `ACTION_ADD_VIDEO` (`edit_playlist`) in die gemeinsame YouTube-Playlist; `PeriodicTimer`-basierter Hintergrund-Auto-Sync (`AutoSyncCollaborativePlaylist`, `AutoSyncPlaylistIntervalSeconds`) plus manueller Sync-Button (`SyncPlaylistNowCommand`).
- **Windows 11 Fluent UI, SystemAccentColor & Backdrops:** Integration von `ApplicationAccentColorManager.ApplySystemAccent()`, `SystemThemeWatcher` mit wählbarem `WindowBackdropType` (`Auto`, `Mica`, `Tabbed`, `Acrylic`, `None`), rechts angedockten Queue-Aktionsbuttons (`DockPanel.Dock="Right"`) und `64x48` Song-Cover in `TrackToastWindow`.
- **EcoQoS & `< 30 MB` RAM Optimierung:** `ProcessEfficiencyManager` aktiviert Windows 11 EcoQoS (`PROCESS_POWER_THROTTLING_EXECUTION_SPEED` / E-Core-Scheduling), `ProcessPriorityClass.Idle`, LOH-Kompaktierung und `K32EmptyWorkingSet`.
- **WebDrop & In-App QR-Code:** Sofort-Start/Stop beim Umschalten in den Einstellungen (`OnEnableWebDropServerChanged`), automatische Port-Fallback-Kette (`8080`, `8081`, `8082`, `8088`, `18080`, `0`), Filterung virtueller Docker/WSL-Netzwerkadapter, `/vote-skip` Endpunkt, Offline-Vektor QR-Code (`/qr.svg` und In-App Fluent-Flyout).
- **Wiedergabe & Queue:** Echte Audio-Wiedergabe via `WpfMediaStreamPlayer` (`System.Windows.Media.MediaPlayer`), Direct Stream Extraction mit `yt-dlp` (priorisierte Suche in AppContext.BaseDirectory, WinGet, PATH) mit Invidious-Fallback, automatische Weiterschaltung auf `MediaEnded`, `Ctrl+V` URL-Paste, Quick-Add Eingabeleiste mit Enter-Taste, Queue Reordering (MoveUp/MoveDown Pfeil-Buttons), automatische Lautstärke-Persistenz (`MasterVolume`).
- **Systray & Single-Instance:** Schnellsteuerung im Tray-Kontextmenü (`Wiedergabe/Pause`, `Nächster Titel`, `Stummschalten`), dynamischer 63-Zeichen Tooltip (`burroQueue: [Titel]`), automatische Windows-Balloon-Benachrichtigungen bei Songwechsel. `SingleInstanceManager` (Win32 Mutex & IPC Event) verhindert Doppelstarts und aktiviert bei erneutem Klick das bestehende Fenster im Vordergrund.
- **Installer, Portable & Authenticode Code-Signing:** Inno Setup 6.x (`scripts/installer/burroQueue.iss`), `scripts/build-release.ps1` (inkl. UnoCSS-Build, optionalem lokalem `signtool.exe`-Signing und `SHA256SUMS.txt`) sowie zweistufiges Open-Source Authenticode Code-Signing (`burroQueue.exe` + `burroQueue-Setup-x64.exe`) über **SignPath.io** / **SignPath Foundation** und GitHub Actions Secrets in `.github/workflows/release.yml` (dokumentiert in `SECURITY.md`).
- **GitHub Governance & Verified Git:** Alle Commits sind kryptografisch per Ed25519 SSH Signing Key signiert (`Verified` auf GitHub). Alle Commits, bestehenden Issues und Roadmap-Features sind im 5-Säulen-Kanban-Board (`https://github.com/users/T-Boyke/projects/7`) mit Milestones (`v1.0.0`–`v2.1.0`), Assignee `T-Boyke`, Sub-Issues (Epics `#23`–`#27`) und Abhängigkeiten (`blockedBy`) organisiert.
- **Architektur-Check:** `./scripts/verify-architecture.ps1` (150/150 Dateien validiert, 0 Violations)
- **CI Formatting:** `dotnet format burroQueue.sln --no-restore --verify-no-changes` (0 Warnungen / 0 Fehler)

---

## 2. Komponenten-Übersicht

### A. Domain (`src/burroQueue.Domain`)

- **Modelle:** `BurroGroup`, `Playlist`, `Track`, `GroupMember`, `AudioVolume`, `SkipVotePool`, `CollaborativePlaylistLink`, `TrackMetadata`.
- **Value Objects:** `GroupId`, `ClientId`, `TrackId`, `PlaybackState`, `TelephonyState`.
- **Domain Events:** `GroupRenamedDomainEvent`, `PlaybackStateChangedDomainEvent`, `TrackMetadataLoadedDomainEvent`, `TrackRemovedDomainEvent`, `VoteToSkipRecordedDomainEvent`, `VoteToSkipThresholdReachedDomainEvent`, `CollaborativePlaylistLinkUpdatedDomainEvent`.
- **Exceptions:** `InvalidTrackUrlException`.

### B. Infrastructure (`src/burroQueue.Infrastructure`)

- **Audio:** `AudioOutputController`, `WindowsAudioDeviceEnumerator`, `AudioDeviceOption`.
- **Playback:** `AudioPlayerBackend`, `YouTubeAudioStreamResolver`, `IStreamUriResolver`.
- **Network:** `LanGroupService` (UDP Multicast `239.255.42.99:41234`), `LocalNetworkInfoService` (LAN IPv4 Discovery).
- **Messages:** `DiscoveryBeaconMessage`, `QueueSyncMessage`, `SkipVoteMessage`, `SharedPlaylistSyncMessage`, `GroupActionMessage`, `PlaybackCommandMessage`.
- **Web:** `EmbeddedWebDropServer` (TCP/HTTP Server Port 8080 mit dynamischem Fallback, `/assets/webdrop.css` UnoCSS 66.12.0 Endpunkt), `WebDropStylesheetProvider`, `QrCodeSvgGenerator`.
- **Telephony:** `TelephonyManager`, `SwyxItTelephonyProvider`, `WindowsCoreAudioTelephonyProvider`, `NullTelephonyProvider`.
- **Persistence:** `PlaylistStorageService` (JSON File Storage in `%AppData%/burroQueue`).
- **Windows:** `WindowsAutostartService` (Registry Run Key Management).
- **YouTube:** `YouTubeUrlParser` (Shorts, Embeds, Live, Music, Timestamps), `YouTubeMetadataFetcher` (oEmbed / Invidious fallback).

### C. Presentation (`src/burroQueue.Presentation`)

- **Views:** `MainWindow.xaml` (Mica, Fluent Design, Taskleisten-Docking), `SettingsWindow.xaml`.
- **ViewModels:** `MainWindowViewModel` (CommunityToolkit.Mvvm, Queue-Binding, Hotkeys, Telephony Ducking, Loopback-Filtering), `SettingsViewModel`.
- **Services:** `SystrayNotificationService`, `GlobalHotkeyService`, `SingleInstanceManager`, `WpfMediaStreamPlayer`.
- **Converters:** `BooleanToVisibilityConverter`, `NullToVisibilityConverter`.

---

## 3. Test-Harness Übersicht

1. **Unit Tests:**
   - 100% Coverage der Domain (Modelle, Value Objects, Events, Exceptions).
   - Ausführliche Tests für alle Infrastructure-Services (`YouTubeUrlParser`, `PlaylistStorageService`, `TelephonyManager`, etc.).
   - Ausführliche Tests für ViewModels, Converter und isolierte View-Logik (`MainWindowViewModelPlaybackTests`, `MainWindowViewModelNetworkSyncTests`, `SettingsViewModelTests`, `MainWindowViewTests`, `AppServiceRegistrationTests`).
2. **Integration Tests:**
   - `WebDropServerIntegrationTests`: Live HTTP GET/POST auf `EmbeddedWebDropServer`.
   - `LanMulticastIntegrationTests`: Lokale UDP-Multicast-Synchronisation zwischen zwei unabhängigen Instanzen.
   - `TelephonyDuckingIntegrationTests`: End-to-End Reaktion auf Anrufe mit Lautstärke-Ducking auf dem AudioPlayerBackend.
3. **E2E System Tests:**
   - `EndToEndWorkflowTests`: Kompletter End-to-End Workflow über den registrierten DI-Container (WebDrop -> Enqueue -> Play -> Skip Vote -> Dateipersistenz -> Wiederherstellung).

---

## 4. Status der Version 1.0

1. **Feature Complete:**
   - Alle Kernfunktionen, Systray Quick-Controls, Offline QR-Code, In-App QR-Flyout, Queue-Reordering, Quick-Add, MasterVolume-Persistenz, Single-Instance Schutz und yt-dlp Auto-Discovery sind vollständig implementiert.
2. **Architektur & Qualität:**
   - 150/150 C#-Dateien nach SFC / 1CPF & reiner DDD-Schichtung validiert.
   - 475/475 Tests auf `.NET 11` bestanden (100% grün).
3. **Release, Code-Signing & Packaging:**
   - Inno Setup 6.x Setup-Installer, Standalone Portable ZIP, `SHA256SUMS.txt` und zweistufige SignPath.io Authenticode-Pipeline via `.github/workflows/release.yml` und `scripts/build-release.ps1`.
