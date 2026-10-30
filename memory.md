# 🫏 burroQueue – Repository Memory

Letzte Aktualisierung: 2026-10-30 | Version: `2.1.0` (Alle 24 Issues & 5 Entwicklungs-Säulen abgeschlossen)

---

## 1. Projekt-Status & Kernmetriken

- **Version:** `2.1.0` (Meilensteine `v1.0.0`, `v1.1.0`, `v1.2.0`, `v2.0.0` und `v2.1.0` vollständig abgeschlossen)
- **Zielsystem:** Windows 11 (Mica, Fluent Design, Systray-Jukebox, Native `SMTC`, Focus Assist, EcoQoS)
- **Runtime:** `.NET 11` / `C# 15` (`net11.0` für Domain/Infrastructure, `net11.0-windows10.0.22621.0` für Presentation/Tests, `<TieredPGO>true</TieredPGO>`)
- **WebDrop Frontend:** Build-Time `UnoCSS 66.12.0` (`src/burroQueue.WebDrop`, Zero Bootstrap, `< 9 KB` Embedded Resource `webdrop.uno.min.css`, Echtzeit-SSE `/api/events`, Mobile Upvote `/upvote` & Collaborator-Avatar)
- **Architektur:** Reine Domain-Driven Design Schichten (Domain = 0 externe Dependencies, 212 C#-Dateien nach SFC / 1CPF validiert)
- **Aktuelle Testanzahl:** > 515 Tests (100% grün auf `net11.0`)
- **Kern-Features (v1.1.0 – v2.1.0):**
  - **Non-Destructive Cursor Queue & Cryptographic CSPRNG Shuffle (`#9`, `#10`):** `Playlist` behält alle gespielten und kommenden Songs (`AllTracks`, `CurrentTrackIndex`, `IsRepeatAllEnabled`, `JumpToTrack`); `QueueShuffleEngine` mischt per `RandomNumberGenerator.Shuffle<T>` (`HardwareCsprngEntropySource`) mit Anti-Repeat-Guard und synchronisiert die Reihenfolge via `QUEUE_ORDER_SYNC`.
  - **YouTube Collaborative Contributor & Circular Avatar Badge (`#18`, `#278`, `#6`):** `InnerTubePlaylistParser` extrahiert per InnerTube `browse` Contributor-Namen und `contributorAvatar` (`yt3.ggpht.com`); `AvatarBitmapCacheService` hält eingefrorene `48x48` Bitmaps im LRU-Cache (`< 150 KB` RAM); `TrackToastWindow` (`320x68`) zeigt das runde `20x20` Collaborator-Badge unten rechts auf dem Cover sowie `SimdSuperResolutionThumbnailEnhancer` (`4x` `64x48 -> 256x192` Upscaling, `#22`).
  - **Zero-Dependency `Span<char>` TOML Persistence (`#13`, `#19`):** `TomlDocumentSerializer` speichert `%LOCALAPPDATA%\burroQueue\settings.toml`, `session.toml` (inkl. `QueuePlaybackCursor` Sekundengenauigkeit + `PreparePausedAtPositionAsync`) sowie kollaborative Gruppen-Favoriten in `favorites.<group>.toml` (`TomlFavoriteQueueRepository`, P2P `TRACK_UPVOTE`).
  - **Smart Clipboard Watcher & 4-Signal `YouTubeMusicValidator` (`#7`):** Event-getriebener Win32 `WM_CLIPBOARDUPDATE`-Listener (`Win32ClipboardMonitorService`, `0%` Idle-CPU) + InnerTube 4-Signal Musik-Validierung (`Off` / `AskToast` / `AutoQueueVerifiedMusic`).
  - **Multi-NIC LAN Discovery, P2P VoIP Ducking, Universal Meeting Detection & Host Handoff (`#15`, `#32`, `#34`, `#35`):** Physische IPv4-NIC-Filterung, aktives `DISCOVER_PROBE` + Unicast-`BEACON`, P2P `PEER_TELEPHONY` Auto-Ducking, Windows 11 Mikrofon-Consent-Erkennung für MS Teams/Zoom/Webex/Browser, `SHQueryUserNotificationState` Focus-Assist-Unterdrückung, Boss-Key `Ctrl+Alt+M` und nahtloser `HOST_HANDOFF` beim Beenden/Abmelden.
  - **`.NET 11` Vector256 SIMD Audio DSP, EBU R128 / LUFS Normalisierung & Deep Work Vibe-Guard (`#20`, `#21`):** `SimdAudioDspProcessor` (`Vector256<float>` RMS/Gain/Soft-Clip + `ComputeEbuR128LinearGain` / `ApplyLoudnessNormalizationToVolume`), InnerTube `loudnessDb`-Extraktion (`-24.0` bis `-6.0` LUFS, Standard `-14.0` LUFS in `SettingsWindow`) und `SimdTrackVibeAnalyzer` (`OfficeVibePolicy` senkt benötigte Skip-Votes bei disruptiven Tracks automatisch auf `1`).
  - **Audio-Only `.m4a` Stream-Pipeline (`0% GPU Video Decode`), Lookahead-Prefetch & Drag-and-Drop Queue:** `YouTubeAudioStreamResolver` lädt reine AAC-Audio-Streams (`140/bestaudio[ext=m4a]`, ~2,5 MB statt ~87 MB MP4-Video), eliminiert den Windows Media Foundation Direct3D11 Video-Decoder (`0% GPU`), lädt den nächsten Queue-Song per `PrefetchTrackAsync` im Hintergrund vor (`< 50 ms` Songwechsel) und erlaubt direktes Drag-and-Drop-Sortieren in `MainWindow.xaml` (`MoveTrackToIndex`).
  - **Live-Lokalisierung (`#16`) & WinGet Pipeline (`#36`):** `.resx` (`de-DE` / `en-US`) via `LocalizationManager` + `{loc:Translate}` zur Laufzeit umschaltbar; WinGet-Manifeste (`scripts/winget/T-Boyke.burroQueue*.yaml`) + `publish-winget` Job in `.github/workflows/release.yml`.

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
