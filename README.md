<p align="center">
  <img src="assets/logo.png" width="140" alt="burroQueue Logo" />
</p>

<h1 align="center">burroQueue</h1>

<p align="center">
  <b>Collaborative Windows 11 Office Jukebox</b><br />
  Featuring VoIP auto-ducking (SwyxIt! & Windows CoreAudio), peer-to-peer LAN discovery, collaborative YouTube playlist sync, and lightweight web drop.
</p>

<p align="center">
  <a href="https://github.com/T-Boyke/burroQueue/actions/workflows/ci.yml">
    <img src="https://github.com/T-Boyke/burroQueue/actions/workflows/ci.yml/badge.svg" alt="Architecture Audit, Build & TDD Suite" />
  </a>
  <img src="https://img.shields.io/badge/Platform-Windows%2011-0078D4?style=flat-square" alt="Platform" />
  <img src="https://img.shields.io/badge/Runtime-.NET%2010%20%7C%20C%23%2014-512BD4?style=flat-square" alt="Runtime" />
  <img src="https://img.shields.io/badge/UI-WPF%20%2B%20Wpf.Ui%20(Mica)-005FB8?style=flat-square" alt="UI" />
  <img src="https://img.shields.io/badge/License-AGPL--3.0-blue.svg?style=flat-square" alt="License" />
</p>

---

## Why "burroQueue"?

In German, an office is a **Büro**. In Spanish, a **burro** is a donkey—a hardworking pack animal that carries heavy loads day in and day out across difficult terrain. In any busy office (*Büro*), coworkers haul through daily sprints, client calls, and engineering deadlines like persistent pack donkeys (*burros*). Yet office music often degenerates into friction: fighting over an AUX cable, locking queues behind proprietary streaming subscriptions, or exposing personal accounts on shared conference hardware.

`burroQueue` resolves this friction by serving as the collective beast of burden. It carries the office musical queue democratically so no single coworker has to act as gatekeeper or DJ. Anyone on the local network can drop a YouTube track into the shared queue via drag-and-drop, clipboard link detection, or a lightweight mobile browser drop. If someone queues a track that disrupts deep work, the office can vote democratically to skip it without conflict.

---

## Key Features

- **VoIP Auto-Ducking (SwyxIt! 11 & Windows CoreAudio):** Monitors incoming and active phone calls via the SwyxIt! Client COM manager (`CLMgr.ClientLineMgr`) and Windows CoreAudio communication sessions. Jukebox volume fades down automatically during calls and smoothly fades back up when the call terminates.
- **Render-Only Audio Device Routing:** Directs music exclusively to dedicated playback devices (office soundbars, Bluetooth speakers, or line-out), actively filtering out microphones and input capture endpoints so headset communication stays isolated.
- **Peer-to-Peer LAN Discovery:** Instances discover each other across the local network using UDP multicast (`239.255.42.99:41234`). Team members can join rooms, rename groups, switch between multiple office groups, and receive unobtrusive toast notifications when colleagues connect.
- **Collaborative YouTube Playlist Synchronization:** Direct support for YouTube collaborative playlist links (`list=...&jct=...`). Features real-time token validation, an invite banner with single-click import, and automatic track polling via an Invidious API engine with multi-instance fallback.
- **Visual Queue & Artwork Display:** Renders queue items with 44x33 album artwork thumbnails, channel names, and durations. Newly added links trigger an auto-dismissing visual status banner (3.5s) confirming the track has been queued.
- **Democratic Vote-to-Skip:** Team members can vote to skip tracks currently playing. Once the consensus threshold is met, the track automatically skips to the next item.
- **Optional Mobile Web-Drop (`http://<PC-IP>:<PORT>`):** Built-in lightweight HTTP listener with dynamic port binding and QR code generation. Coworkers on iOS, Android, macOS, or Linux can drop tracks into the queue without installing any desktop software (server is disabled by default).
- **Systray Operation & Minimal Resource Usage:** Minimizes cleanly to the Windows 11 system tray with quick playback controls and an `About burroQueue` dialog. Operates at under 30 MB RAM idle by utilizing native WPF and avoiding WebView2 or Electron wrappers.

---

## Architecture & Code Quality

burroQueue enforces a 4-layer Domain-Driven Design (DDD) architecture and strict Single Component Per File (1CPF) rules:

```
src/
├── burroQueue.Domain/          # Pure core business models, events, and exceptions (0 external dependencies)
├── burroQueue.Infrastructure/  # Audio, YouTube API, P2P network sockets, VoIP, and local HTTP listener
├── burroQueue.Presentation/    # WPF Fluent UI (Mica), ViewModels (CommunityToolkit.Mvvm), Systray service
└── burroQueue.Tests/           # Comprehensive xUnit, FluentAssertions, and NSubstitute test suite
```

### The 5 Architectural Laws

1. **Pure Domain (DDD):** `burroQueue.Domain` contains core queue, track, voting, and playlist logic. It references **zero** external NuGet packages and **zero** UI or networking namespaces.
2. **Test-Driven Development (TDD):** Every bugfix and feature begins with a failing xUnit test in `src/burroQueue.Tests` before writing production code.
3. **Single Component Per File (1CPF):** Every `.cs` file contains exactly one type definition (class, interface, struct, record, or enum). Filenames match the type name exactly.
4. **Performance First (< 30 MB RAM Standby):** No Chromium/Electron wrappers, no polling loops, and memory-conscious value objects (`readonly record struct`).
5. **Atomic Conventional Commits:** All changes are committed in small, logical increments adhering to Conventional Commits 1.0.0.

Verify architecture compliance at any time:
```powershell
./scripts/verify-architecture.ps1
```

---

## Quickstart

### Prerequisites
- Windows 11 (22H2 or newer recommended)
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

### Build and Test
```powershell
# Clone repository
git clone https://github.com/T-Boyke/burroQueue.git
cd burroQueue

# Verify architecture constraints (DDD purity & 1CPF)
./scripts/verify-architecture.ps1

# Build solution with warnings treated as errors
dotnet build burroQueue.sln --configuration Release -warnaserror

# Run unit and integration tests
dotnet test burroQueue.sln --configuration Release
```

### Launch
```powershell
dotnet run --project src/burroQueue.Presentation/burroQueue.Presentation.csproj
```

---

## CI/CD Workflows

- **Continuous Integration:** [`.github/workflows/ci.yml`](.github/workflows/ci.yml) executes architecture validation, formatting verification (`dotnet format --verify-no-changes`), release compilation with `-warnaserror`, and code coverage tests on Windows runners.
- **Release Automation:** [`.github/workflows/release.yml`](.github/workflows/release.yml) publishes self-contained Windows 11 x64 binaries on Git version tags.
- **Self-Hosted Runner:** Setup script available at [`scripts/runner/setup-runner.ps1`](scripts/runner/setup-runner.ps1) for automated local deployment testing.

---

## License

This project is licensed under the **GNU Affero General Public License v3.0 (AGPL-3.0)**. See the [LICENSE](LICENSE) file for the complete license terms.
