# 🫏 burroQueue

**burroQueue** ist eine winzige, hochperformante und native Windows 11 Taskleisten-App (Systray), die als kollaborative Office-Jukebox fungiert. Eine humorvolle Anlehnung an den Büroalltag und den unermüdlichen "Arbeitselel" (*Burro*): Mitarbeiter können YouTube-Links per Drag-and-Drop oder Zwischenablage einfach in die Queue "hineinwerfen". Diese werden automatisch in eine zentrale Playlist des Hosts eingespeist und über dessen Audioausgabe (z. B. Bluetooth-Box) abgespielt.

Die App fügt sich nahtlos in das **Windows 11 Fluent Design (Mica/Acrylic, Light/Dark Mode)** ein.

---

## 🏛️ Architektur & Prinzipien

Dieses Projekt wurde mit Fokus auf extreme Wartbarkeit, Testbarkeit und Performance entwickelt. Folgende Paradigmen sind zwingend einzuhalten:

*   **Domain-Driven Design (DDD):** Kapselung der Kernlogik (Playlist, Track, Host) in einem reinen, framework-unabhängigen Domain-Layer.
*   **Test-Driven Development (TDD):** Jedes Feature wird durch Unit- und Integrationstests abgesichert. Keine Code-Änderung ohne fehlschlagenden Test vorab.
*   **Clean Code & SOLID:** Strict Separation of Concerns, Dependency Injection und minimale Seiteneffekte.
*   **SFC / 1CPF (Single Component Per File):** Jede Datei enthält exakt eine Klasse, ein Interface oder eine UI-Komponente, um maximale Übersicht zu gewährleisten.

---

## 🏗️ Projektstruktur

```text
src/
├── burroQueue.Domain/           # Reine Geschäftslogik (Entities, Value Objects, Domain Events)
│   ├── Model/
│   │   ├── Playlist.cs
│   │   └── Track.cs
│   └── Exceptions/
├── burroQueue.Infrastructure/   # Externe Services (YouTube API, Network Listener, Config)
│   ├── YouTube/
│   └── Network/
├── burroQueue.Presentation/     # Native WPF + Wpf.Ui App / Taskbar Integration
│   ├── ViewModels/
│   ├── Views/
│   └── App.xaml.cs
└── burroQueue.Tests/            # TDD Test-Suite (XUnit / FluentAssertions)
```

---

## 🚀 Features

- 🪟 **Native Win11 UI via Wpf.Ui:** Unterstützt echtes Mica-Material, abgerundete Ecken und automatischen Wechsel zwischen Light & Dark Mode.
- 🎯 **Taskbar Drop-Zone:** Minimale Beeinträchtigung des Workflows. Link reinziehen – fertig.
- 📡 **Local Network Sync:** Mitarbeiter benötigen keine Installation. Ein leichtgewichtiges Web-Interface oder ein lokaler API-Endpoint nimmt die Links entgegen.
- 🔋 **Performance First:** Extrem geringer RAM- und CPU-Verbrauch (<30MB RAM im Standby) durch nativen .NET-WPF-Unterbau ohne schwere Electron/Web-Wrapper.

---

## 🛠️ Tech Stack & Voraussetzungen

- **Sprache:** C# 14 / .NET 10+
- **UI Framework:** WPF + Wpf.Ui
- **Architektur:** MVVM (CommunityToolkit.Mvvm)

---

## 🏁 Schnellstart (Entwicklung)

1. **Repository klonen:**
   ```bash
   git clone https://github.com
   ```
2. **Tests ausführen (TDD-Workflow):**
   ```bash
   dotnet test
   ```
3. **Host-App starten:**
   Öffne die Solution in Visual Studio 2025 und starte das Projekt `burroQueue.Presentation`.

---

## 📄 Lizenz

Dieses Projekt ist unter der **GNU Affero General Public License v3.0 (AGPL-3.0)** lizenziert. Siehe die [LICENSE](LICENSE) Datei für Details.
