# 🫏 burroQueue – Repository Memory

Letzte Aktualisierung: 2026-10-27 | Ziel: Stabile Version 1.0 (Produktionsreife)

---

## 1. Projekt-Status & Kernmetriken

- **Version:** `1.0.0-rc`
- **Zielsystem:** Windows 11 (Mica, Fluent Design, Systray-Jukebox)
- **Runtime:** .NET 10 (`net10.0` für Domain/Infrastructure/Tests, `net10.0-windows10.0.22621.0` für Presentation)
- **Architektur:** Reine Domain-Driven Design Schichten (Domain = 0 externe Dependencies, Single Component Per File)
- **Aktuelle Testanzahl:** 339 Tests (100% grün)
- **Aktuelle Code-Coverage:**
  - `burroQueue.Domain`: 100,00% Line Coverage (97,05% Branch)
  - `burroQueue.Infrastructure`: 97,8% Line Coverage (86,5% Branch)
  - `burroQueue.Presentation`: 98,6% Line Coverage (87,2% Branch)
- **Installer & Portable:** Inno Setup 6.x + Portable Single-File Release Build (`./publish/portable/burroQueue.exe`)
- **Lifecycle & Systray:** `ShutdownMode.OnExplicitShutdown` verhindert versehentliches Schließen während asynchroner Initialisierung oder beim Minimieren in den Systray. `SystrayNotificationService` nutzt automatisches Fallback auf `Icon.ExtractAssociatedIcon(Environment.ProcessPath)` im Single-File-Modus.
- **Architektur-Check:** `./scripts/verify-architecture.ps1` (123/123 Dateien validiert, 0 Violations)
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
- **Services:** `SystrayNotificationService`, `GlobalHotkeyService`.
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

## 4. Aktueller Ausbauplan zu 1.0

1. **Playlist Auto-Persistence im UI-Flow:**
   - Automatische Speicherung der Queue bei Enqueue-, Remove- und Track-Advance-Ereignissen im `MainWindowViewModel`.
   - Automatisches Laden persistierter Titel beim Start.
2. **Global Hotkeys & Systray-Integration:**
   - Erweiterte Unit-Tests für `GlobalHotkeyService` und `SystrayNotificationService`.
   - Zusammenspiel von Minimieren ins Tray und Hotkey-Event-Handling absichern.
3. **Code-Coverage weiter ausbauen:**
   - Weitere Randfälle in Infrastructure und Presentation abdecken (Coverage > 85-90% gesamt).
4. **Git-Veröffentlichung:**
   - Alle Commits sauber auf `origin/main` pushen.
