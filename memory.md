# 🫏 burroQueue – Repository Memory

Letzte Aktualisierung: 2026-10-27 | Ziel: Stabile Version 1.0 (Produktionsreife)

---

## 1. Projekt-Status & Kernmetriken

- **Version:** `1.0.0-rc`
- **Zielsystem:** Windows 11 (Mica, Fluent Design, Systray-Jukebox)
- **Runtime:** .NET 10 (`net10.0` für Domain/Infrastructure/Tests, `net10.0-windows10.0.22621.0` für Presentation)
- **Architektur:** Reine Domain-Driven Design Schichten (Domain = 0 externe Dependencies, Single Component Per File)
- **Aktuelle Testanzahl:** 69 Tests (alle grün)
- **Aktuelle Code-Coverage:** 42,66% Line Coverage (Ziel: ~100% über Unit, Integration, System, E2E)
- **Installer:** Inno Setup 6.x mit unkomprimiertem 32bpp DIB Multi-Res `.ico` (16–256 px)

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
- **Web:** `EmbeddedWebDropServer` (HTTP Server Port 8080 für No-Install Drop & Queue-View).
- **Telephony:** `TelephonyManager`, `SwyxItTelephonyProvider`, `WindowsCoreAudioTelephonyProvider`, `NullTelephonyProvider`.
- **Persistence:** `PlaylistStorageService` (JSON File Storage in `%AppData%/burroQueue`).
- **Windows:** `WindowsAutostartService` (Registry Run Key Management).
- **YouTube:** `YouTubeUrlParser`, `YouTubeMetadataFetcher` (oEmbed / Invidious fallback).

### C. Presentation (`src/burroQueue.Presentation`)

- **Views:** `MainWindow.xaml` (Mica, Fluent Design, Taskleisten-Docking), `SettingsWindow.xaml`.
- **ViewModels:** `MainWindowViewModel` (CommunityToolkit.Mvvm, Queue-Binding, Hotkeys, Telephony Ducking), `SettingsViewModel`.
- **Services:** `SystrayNotificationService`, `GlobalHotkeyService`.
- **Converters:** `BooleanToVisibilityConverter`, `NullToVisibilityConverter`.

---

## 3. Aktueller Ausbauplan zu 1.0

1. **ADRs dokumentieren (`decisions.md`):** Alle Kernentscheidungen festhalten.
2. **Settings & Auto-Persistence Refactoring:**
   - Playlist speichert sich bei jeder Enqueue/Remove/Advance Operation automatisch.
   - `SettingsViewModel` voll an `IAppSettingsService`, `IAudioDeviceEnumerator` und `IWindowsAutostartService` anbinden.
3. **Global Hotkeys & Systray:**
   - Tastenkombinationen (`MediaPlayPause`, `MediaNextTrack`, etc.) registrieren und testen.
   - Systray-Notifications bei Trackwechsel und Skip-Votes.
4. **YouTube URL Parser Erweiterung:**
   - Vollständige Unterstützung für YouTube Music, Shorts, Timestamps (`t=...`) und Bereinigung von Tracking-Parametern (`si=...`).
5. **Umfassendes Test-Harness (Ziel 100%):**
   - Unit Tests für alle verbleibenden Typen in Domain, Infrastructure und Presentation.
   - Integrationstests für Netzwerk (Multicast), WebDrop (HTTP), Telephony-Ducking und Storage Roundtrip.
   - System & E2E Tests mit vollständigem Flow (DI -> Drop -> P2P -> Ducking -> Skip -> Persist).
6. **Repository & CI Ausbau:**
   - GitHub Actions CI mit Coverage-Reporting, Architektur-Gate und Release-Build.
   - Issue- und PR-Templates.
