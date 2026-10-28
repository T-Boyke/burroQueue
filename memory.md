# 🫏 burroQueue – Repository Memory

Letzte Aktualisierung: 2026-10-28 | Ziel: Stabile Version 1.0 (Produktionsreife)

---

## 1. Projekt-Status & Kernmetriken

- **Version:** `1.0.0` (Feature Complete & Produktionsreif)
- **Zielsystem:** Windows 11 (Mica, Fluent Design, Systray-Jukebox)
- **Runtime:** .NET 10 (`net10.0` für Domain/Infrastructure/Tests, `net10.0-windows10.0.22621.0` für Presentation)
- **Architektur:** Reine Domain-Driven Design Schichten (Domain = 0 externe Dependencies, Single Component Per File)
- **Aktuelle Testanzahl:** 436+ Tests (100% grün)
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
- **Installer & Portable:** Inno Setup 6.x (`scripts/installer/burroQueue.iss`), automatisiertes One-Click Build-Skript `scripts/build-release.ps1` (erzeugt Portable Single-File EXE und ZIP).
- **Architektur-Check:** `./scripts/verify-architecture.ps1` (132/132 Dateien validiert, 0 Violations)
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
- **Web:** `EmbeddedWebDropServer` (HTTP Server Port 8080 für No-Install Drop & Queue-View mit dynamischem Fallback).
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
   - 132/132 C#-Dateien nach SFC / 1CPF & reiner DDD-Schichtung validiert.
   - 399/399 Tests bestanden (100% grün).
3. **Release & Packaging:**
   - Inno Setup 6.x Setup-Installer und Standalone Portable ZIP via `scripts/build-release.ps1`.
