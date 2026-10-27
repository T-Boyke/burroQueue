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
