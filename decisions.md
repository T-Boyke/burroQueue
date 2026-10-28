# 🫏 burroQueue – Architecture Decision Records (ADR)

Dieses Dokument hält alle zentralen architektonischen Entscheidungen für `burroQueue` fest.

---

## ADR-001: Reine Domain (DDD) ohne externe Abhängigkeiten

- **Status:** Angenommen
- **Kontext:** Die Geschäftslogik (`Playlist`, `Track`, `SkipVotePool`, `AudioVolume`) soll langlebig, maximal testbar und unabhängig von UI-, Betriebssystem- und Netzwerk-Frameworks sein.
- **Entscheidung:** `burroQueue.Domain` besitzt exakt **0** NuGet-Paket-Abhängigkeiten und **0** Projekt-Referenzen. Weder `System.Windows.*`, `Wpf.Ui`, noch `System.Net` dürfen im Domain-Projekt referenziert werden.
- **Konsequenzen:** Alle Geschäftsregeln können in Millisekunden per Unit-Tests isoliert validiert werden. Das Skript `./scripts/verify-architecture.ps1` erzwingt diese Reinheit bei jedem CI-Lauf.

---

## ADR-002: Single Component Per File (1CPF)

- **Status:** Angenommen
- **Kontext:** Große Code-Dateien mit verschachtelten Hilfsklassen, Enums oder privaten Interfaces erschweren Code-Reviews, Navigation und Wartbarkeit.
- **Entscheidung:** Jede `.cs`- und `.xaml`-Datei enthält exakt **eine** Komponente (1 `class`, 1 `interface`, 1 `struct`, 1 `record` oder 1 `enum`). Der Dateiname muss exakt dem Typnamen entsprechen.
- **Konsequenzen:** Hervorragende Übersichtlichkeit, Git-Diffs bleiben minimal und punktgenau. Verstöße werden durch `./scripts/verify-architecture.ps1` sofort blockiert.

---

## ADR-003: P2P LAN-Synchronisation via UDP Multicast (`239.255.42.99:41234`)

- **Status:** Angenommen
- **Kontext:** Im Büro sollen Kollegen im selben Subnetz gemeinsame Wiedergabelisten führen und Skip-Votes abgeben, ohne dass ein dedizierter Cloud-Server oder zentraler Host eingerichtet werden muss.
- **Entscheidung:** Nutzung von UDP-Multicast über die reservierte lokale Gruppenadresse `239.255.42.99` auf Port `41234`. JSON-Nutzlasten für `BEACON`, `QUEUE_SYNC` und `SKIP_VOTE`.
- **Konsequenzen:** Keine Server-Kosten, kein Setup für Anwender, sofort einsatzbereit bei Rechnerstart.

---

## ADR-004: Embedded WebDrop HTTP-Server für Zero-Install Zugriff

- **Status:** Angenommen
- **Kontext:** Nicht jeder Kollege im Büro möchte oder darf die Desktop-Anwendung installieren, soll aber dennoch YouTube-Links in die Queue werfen können.
- **Entscheidung:** Ein integrierter, leichtgewichtiger HTTP-Server (`HttpListener`) auf Port `8080`, der unter der tatsächlichen LAN-IPv4 erreichbar ist. Er liefert ein schlankes, reaktives WebDrop-Interface (inklusive Queue-Status und AGPL-3.0 Quellcode-Link) aus und nimmt POST-Drops entgegen.
- **Konsequenzen:** Kollegen können per Smartphone oder beliebigem Browser Songs einreichen. Der Server verbraucht im Leerlauf unter 1 MB RAM.

---

## ADR-005: Ressourcenschonendes Audio-Playback mit Telephony Ducking

- **Status:** Angenommen
- **Kontext:** Jukebox-Musik muss bei eingehenden Telefonaten (z. B. SwyxIt oder Windows Core Audio) sofort unaufdringlich leiser werden (Ducking) und bei Gesprächsende wieder zur Ziel-Lautstärke zurückkehren. Speicherverbrauch muss unter 30 MB bleiben (kein WebView2).
- **Entscheidung:** Trennung von Audio-Steuerung (`AudioOutputController`) und Player-Backend (`AudioPlayerBackend`). Modulare Telephony-Provider überwachen Telefoniezustände und triggern das Absenken der Lautstärke.
- **Konsequenzen:** Keine Audio-Störungen bei Kundentelefonaten im Büro, volle Reaktivität ohne UI-Thread-Blockierung.

---

## ADR-006: Multi-Resolution 32bpp DIB `.ico` für Inno Setup

- **Status:** Angenommen
- **Kontext:** Der Windows PE Resource Updater in Inno Setup (`iscc.exe`) schlägt fehl, wenn `.ico`-Dateien komprimierte PNG-Frames in inkompatiblen Größen oder unpassende Header enthalten.
- **Entscheidung:** Automatische Generierung einer standardkonformen unkomprimierten Multi-Resolution DIB `.ico` Datei (16, 24, 32, 48, 64, 128, 256 px) mittels PowerShell GDI+ Skript [`scripts/generate-app-icon.ps1`](file:///d:/GitHub/burroQueue/scripts/generate-app-icon.ps1).
- **Konsequenzen:** Zuverlässige, reproduzierbare Installer-Builds in GitHub Actions ohne externe Icon-Editoren.

---

## ADR-007: Test-Pyramide für 100% Code-Abdeckung

- **Status:** Angenommen
- **Kontext:** Die Anwendung soll hochgradig robust, fehlertolerant und wartungsarm sein.
- **Entscheidung:** 4-Stufige Testpyramide:
  1. **Unit Tests:** Schnelle, isolierte Tests für jeden einzelnen Typ in Domain, Infrastructure und ViewModels.
  2. **Integration Tests:** Verifikation echter Socket-Multicasts, HTTP-Drops, Dateisystem-Persistenz und Telephony-Ducking.
  3. **System Tests:** End-to-End Zusammenspiel der Komponenten über den DI-Container.
  4. **Architektur-Tests:** Automatisierte Durchsetzung aller 1CPF- und DDD-Regeln via CI-Skript.
- **Konsequenzen:** Regressionsfreiheit bei jedem Release, hohe Zuversicht für kontinuierliche Weiterentwicklung.

---

## ADR-008: Loopback-Filterung für Multicast-Nachrichten in MainWindowViewModel

- **Status:** Angenommen
- **Kontext:** Lokale UDP-Multicast-Pakete (z. B. `PlaybackCommandMessage`, `GroupActionMessage`, `SkipVoteMessage`) werden durch Windows Loopback an denselben Rechner zurückgespiegelt. Ohne Filterung würden Befehle wie `Skip` oder `Rename` auf dem Sender doppelt ausgeführt.
- **Entscheidung:** Jede gesendete Multicast-Nachricht enthält die `ClientId` des Absenders. Der Empfänger prüft `if (message.IssuerClientId == _localClientId.Value) return;` und ignoriert eigene gespiegelte Pakete.
- **Konsequenzen:** Absolute Stabilität im lokalen Netzwerkbetrieb ohne Doppelausführungen.

---

## ADR-009: Isolierte statische View-Logik-Handler für UI- und Drag&Drop-Tests

- **Status:** Angenommen
- **Kontext:** WPF `FluentWindow`-Komponenten nutzen Windows 11 Fluent Mica-Backdrops und Win32 DWM APIs, die in CI- und Headless-Testläufern ohne aktiven Desktop Window Manager zu Native-Crashes oder unhandled Win32-Exceptions führen können. Zudem haben WPF `DragEventArgs` geschützte interne Konstruktoren.
- **Entscheidung:** Sämtliche nicht-triviale Ereignis-Logik von Fenstern (wie Drag-and-Drop Parsing, Close-to-Tray Abfang-Routinen und globale Hotkey-Verdrahtung) wird als statische interne Methoden (`HandleDragOver`, `HandleDrop`, `HandleClosing`, `HandleHotkeyRegistration`) extrahiert, die standardmäßige Schnittstellen wie `IDataObject`, `CancelEventArgs` und `IGlobalHotkeyService` entgegennehmen.
- **Konsequenzen:** 100% deterministische, robuste und blitzschnelle Unit-Tests ohne XAML-Window-Initialisierung oder Flakiness in headless Testläufen.

---

## ADR-010: Robuste HttpListener-Instanziierung mit dynamischem Fallback

- **Status:** Angenommen
- **Kontext:** `HttpListener` verlangt unter Windows Administrator-Rechte für Wildcard-Präfixe (`http://*:8080/`). Scheitert der Start, wird die Instanz von der .NET-Runtime als verworfen markiert und wirft bei Wiederverwendung eine `ObjectDisposedException`.
- **Entscheidung:** Bei Fehlschlagen des Wildcard-Präfixes wird eine frische `HttpListener`-Instanz mit Präfix `http://localhost:{Port}/` erzeugt und gestartet.
- **Konsequenzen:** Zuverlässiger Serverstart sowohl mit als auch ohne Administrator-Rechte in allen Benutzer- und Testumgebungen.

---

## ADR-011: Entkopplung von Plattform- und Win32-APIs via interne Test-Delegates

- **Status:** Angenommen
- **Kontext:** Dienste mit Systemabhängigkeiten (z. B. `Clipboard`, `Process.Start`, `Dispatcher`, `RegisterHotKey`, `Activator.CreateInstance` für COM) können in headless CI-Umgebungen nicht direkt ausgeführt werden oder werfen ohne Desktop-Sitzung Exceptions.
- **Entscheidung:** Relevante Plattformaufrufe werden in internen Delegaten mit Produktions-Standardwerten gekapselt (z. B. `ClipboardAction`, `ProcessStarter`, `UiDispatcherRunner`, `ComActivator`, `WindowHandleProvider`). In Unit-Tests können diese punktuell überschrieben werden, um sowohl Erfolgs- als auch Ausnahme-Pfade deterministisch zu verifizieren.
- **Konsequenzen:** Nahezu 100% Testabdeckung in Infrastructure und Presentation ohne schwerfällige Wrapper-Klassen oder Verletzungen der 1CPF/DDD-Regeln.

---

## ADR-012: Anti-AI-Slop WebDrop mit Fluent Dark Design, Submitter-Identität und Live-Polling

- **Status:** Angenommen
- **Kontext:** Der WebDrop-Server für Büro-Mitarbeiter soll weder generisch noch wie ein AI-Slop-Template wirken (keine Emojis wie `🫏`, kein grober SaaS-Cyan-Stil, keine rohen Text-Bestätigungsseiten). Zudem sollen Einreicher ihren Namen angeben können und über das Web-UI mobil für Skips stimmen können.
- **Entscheidung:**
  1. Gestaltung im Windows 11 Fluent Dark Look (`Segoe UI Variable`, saubere Surface-Hierarchie, dezente Status-Toasts, semantische Typografie).
  2. Submitter-Eingabe mit clientseitiger Speicherung in `localStorage`.
  3. POST-Endpunkte (`/drop`, `/vote-skip`) antworten mit `HTTP 303 See Other` statt rohem HTML (verhindert doppeltes Einwerfen bei Browser-Aktualisierung).
  4. Bereitstellung von `GET /api/status` und 5-Sekunden Vanilla JS Polling zur dynamischen Queue- und Now-Playing-Aktualisierung im Browser ohne Neuladen.
- **Konsequenzen:** Hochwertiges, responsives Benutzererlebnis auf Smartphones und Desktop-Browsern ohne zusätzliche Frontend-Frameworks.

---

## ADR-013: Abstraktion des Audio-Stream-Playbacks (IMediaStreamPlayer) und Queue Auto-Advance

- **Status:** Angenommen
- **Kontext:** burroQueue soll echte Audiodaten über das konfigurierte Windows-Audiogerät wiedergeben und nach Ablauf eines Songs automatisch zum nächsten Song der Queue weiterschalten, während die Trennung zwischen Infrastructure (`net10.0`) und Presentation (`WPF`) sauber gewahrt bleibt.
- **Entscheidung:**
  1. `burroQueue.Infrastructure` definiert die Schnittstelle `IMediaStreamPlayer` (`Open`, `Play`, `Pause`, `StopPlayback`, `Volume`, `MediaEnded`).
  2. `AudioPlayerBackend` steuert den Player und reagiert auf `MediaEnded`, um via `TrackPlaybackFinished` die automatische Weiterschaltung (`SkipAsync()`) im ViewModel auszulösen.
  3. `burroQueue.Presentation` implementiert `WpfMediaStreamPlayer` über `System.Windows.Media.MediaPlayer` (PresentationCore, 0 zusätzliche Fremdpakete).
  4. `YouTubeAudioStreamResolver` löst Audio-Streams nativ über `yt-dlp` (mit Invidious-API-Fallback) zu direkten Audio-Streams auf.
- **Konsequenzen:** Echte, ressourcenschonende Musikwiedergabe mit automatischer Weiterschaltung und 100% Testbarkeit ohne Native-Audio-Hänger in CI-Pipelines.

---

## ADR-014: Systray Quick-Controls, dynamischer Tooltip und Songwechsel-Benachrichtigungen

- **Status:** Angenommen
- **Kontext:** Als residente Taskleisten-/Systray-App soll burroQueue im Hintergrund bedienbar sein, ohne dass das Hauptfenster geöffnet werden muss. Anwender wollen sehen, welcher Titel läuft, und Medien direkt über das Kontextmenü des Tray-Icons steuern.
- **Entscheidung:**
  1. `ISystrayNotificationService` bietet Callbacks für `Wiedergabe / Pause`, `Nächster Titel (Skip)` und `Stummschalten (Mute)` direkt im Systray-Kontextmenü.
  2. Dynamische Aktualisierung von `NotifyIcon.Text` mit sicherem 63-Zeichen-Truncating für Windows Shell Limits (`burroQueue: [Titel]`).
  3. Automatische Windows-Balloon/Toast-Benachrichtigungen via `ShowNotification` bei jedem Songwechsel.
- **Konsequenzen:** Erstklassige native Windows 11 Desktop-Integration mit vollständiger Blindbedienung aus dem Infobereich.

---

## ADR-015: Offline QR-Code Generierung für mobilen WebDrop und Queue Reordering

- **Status:** Angenommen
- **Kontext:** Das Abtippen lokaler IPv4-Adressen (`http://192.168.x.x:8080`) auf Smartphones ist fehleranfällig. Zudem soll die Warteschlange im Hauptfenster bequem sortiert werden können.
- **Entscheidung:**
  1. Eigenständige, 100% offline funktionierende QR-Code SVG-Generierung (`IQrCodeSvgGenerator`, `QrCodeSvgGenerator`) nach ISO/IEC 18004 ohne externe Fremdpakete.
  2. Bereitstellung eines schlanken `/qr.svg` Endpunkts und direkte Einbettung in den WebDrop-Header.
  3. Erweiterung von `Playlist` um `MoveUp(TrackId)` und `MoveDown(TrackId)` mit entsprechenden Fluent-Pfeil-Buttons im WPF UI und Quick-Add Linkleiste mit Enter-Unterstützung.
- **Konsequenzen:** Null zusätzliche NuGet-Abhängigkeiten, reibungsloser mobiler Zugriff für Kollegen im Büro und vollständige Kontrolle über die Queue-Reihenfolge.

---

## ADR-016: Single-Instance Mutex, MasterVolume-Persistenz, In-App QR-Code und yt-dlp App-Verzeichnis-Erkennung

- **Status:** Angenommen
- **Kontext:** Für die finale Produktionsreife 1.0 müssen versehentliche Mehrfachstarts (Doppelklick im Systray-Betrieb) verhindert, persönliche Lautstärkeeinstellungen über Restarts hinweg gemerkt, mobile Kollegen direkt vom Desktop-Bildschirm per QR-Code angebunden und portable yt-dlp Installationen im Programmverzeichnis ohne PATH-Konfiguration erkannt werden.
- **Entscheidung:**
  1. `ISingleInstanceManager` & `SingleInstanceManager` mit benanntem Win32 Mutex (`burroQueue_SingleInstance_Mutex`) und `EventWaitHandle` (`burroQueue_Show_Event`). Eine Zweitinstanz signalisiert die Erstinstanz zur Fenster-Wiederherstellung und beendet sich sofort sauber.
  2. Erweiterung von `AppSettingsData` um `MasterVolume` (0.0 - 1.0) mit automatischem Speichern im `MainWindowViewModel` und Wiederherstellung beim Start.
  3. Direktes In-App QR-Code Flyout in `MainWindow.xaml` mit nativer Vektor-/Bitmap-Darstellung über `IQrCodeSvgGenerator.GenerateMatrix` ohne externe NuGet-Pakete.
  4. Ergänzung von `AppContext.BaseDirectory` als priorisierte Suchstelle in `YouTubeAudioStreamResolver.FindYtDlp()`.
  5. Transparente System- & Audio-Diagnostik im Einstellungsfenster (`SettingsWindow.xaml`) für Audio-Engine (`yt-dlp` vs. API-Fallback), WebDrop-Endpunkt und P2P-Multicast.
- **Konsequenzen:** Höchste Betriebsstabilität, kein Anwender-Frust durch Lautstärke-Resets oder Port-Kollisionen und erstklassige Benutzerfreundlichkeit.

---

## ADR-017: STARFACE VoIP 3-Stufen-Erkennung, Ducking Release Delay, Bidirektionaler Playlist-Sync, EcoQoS & Fluent System-Themes

- **Status:** Angenommen
- **Kontext:** Erweiterung der Büro-Jukebox um STARFACE VoIP-Erkennung, verzögerte Lautstärke-Wiederherstellung nach Telefonaten, dauerhaftes Zurückschreiben neuer Songs in kollaborative YouTube-Playlists, E-Core/RAM-Effizienz (`< 30 MB`) sowie Windows 11 System-Akzentfarben und konfigurierbare Window-Backdrops.
- **Entscheidung:**
  1. `StarfaceTelephonyProvider` kombiniert WCF Named-Pipe-Probing (`StarfaceUcClient/UccService`), inkrementelles Log-Tailing (`UCC*.log`) und CoreAudio-Prozess-Sessions mit einer im UI einstellbaren Nachlaufzeit (`DuckingReleaseDelaySeconds`).
  2. `YouTubePlaylistWriter` persistiert neue Queue-Tracks via YouTube InnerTube `edit_playlist` (`ACTION_ADD_VIDEO`), ergänzt um ein periodisches Auto-Sync-Intervall (`AutoSyncPlaylistIntervalSeconds`).
  3. `ProcessEfficiencyManager` nutzt Win32 `SetProcessInformation` / `SetThreadInformation` (`EcoQoS`) für Intel/AMD E-Cores sowie `K32EmptyWorkingSet` zur Einhaltung des `< 30 MB` RAM-Ziels.
  4. `MainWindow` und `SettingsWindow` binden `ApplicationAccentColorManager` (`SystemAccentColor*Brush`) und `SystemThemeWatcher` mit wählbarem `WindowBackdropType` (`Auto`, `Mica`, `Tabbed`, `Acrylic`, `None`) ein; Queue-Aktionsicons werden rechtsbündig im `DockPanel` ausgerichtet und `TrackToastWindow` zeigt das `64x48` Song-Cover.
  5. `SettingsViewModel.OnEnableWebDropServerChanged` startet/stoppt den `EmbeddedWebDropServer` unmittelbar beim Umschalten inklusive Port-Fallback und Filterung virtueller Docker/WSL-Subnetze.
- **Konsequenzen:** Nahtlose Integration in moderne Windows 11 Büro-Umgebungen mit minimalem Ressourcenverbrauch und zuverlässiger Erreichbarkeit des WebDrop-Servers.
