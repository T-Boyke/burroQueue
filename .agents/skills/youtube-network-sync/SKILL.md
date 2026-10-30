---
name: youtube-network-sync
description: >-
  Guides the implementation of YouTube URL/InnerTube parsing, UDP Multicast LAN discovery, and the
  build-time UnoCSS 66.12.0 WebDrop server in burroQueue.Infrastructure. Use this skill when working
  on YouTube link extraction, playlist sync, local HTTP/SSE WebDrop endpoints, or AGPL-3.0 compliance.
---

# YouTube, LAN Sync & UnoCSS 66.12.0 WebDrop Skill (`burroQueue.Infrastructure`)

Dieser Skill definiert die Vorgaben für YouTube-, LAN- und WebDrop-Integrationen in `src/burroQueue.Infrastructure` und `src/burroQueue.WebDrop`.

## 1. YouTube-Link-Verarbeitung & Bidirektionaler Playlist-Sync
- Unterstützte URL-Formate müssen ohne Regex-Backtracking-Risiko (`Uri`-Parsing + `[GeneratedRegex]`) erkannt werden:
  - `https://www.youtube.com/watch?v=<11-char-id>`
  - `https://youtu.be/<11-char-id>`
  - `https://music.youtube.com/watch?v=<11-char-id>`
  - `https://www.youtube.com/shorts/<11-char-id>`
- Jede Komponente (`IYouTubeUrlParser`, `YouTubeUrlParser`, `YouTubePlaylistReader`, `YouTubePlaylistWriter`) liegt gemäß **1CPF** in einer eigenen Datei.
- Alle Parser- und Sync-Fälle müssen per xUnit (`[Fact]` / `[Theory]`) in `src/burroQueue.Tests` abgedeckt sein.

## 2. Build-Time `UnoCSS 66.12.0` WebDrop-Pipeline (Zero Bootstrap, `< 30 MB` RAM)
- Mitarbeiter im Büro werfen ohne lokale App-Installation Songs über `EmbeddedWebDropServer` (`TcpListener`, Standard-Port `8080` mit Port-Fallback und Filterung virtueller WSL/Hyper-V/Docker-Adapter) ein.
- **Strikte Styling-Architektur (`UnoCSS 66.12.0`):**
  - **Kein Bootstrap**, keine Inline-`<style>`-Blöcke in C#, kein `WebView2` und kein Node.js-Prozess zur App-Laufzeit!
  - Alle WebDrop-Styles und Windows 11 Fluent Light/Dark Design-Tokens (`prefers-color-scheme`) liegen unter `src/burroQueue.WebDrop/` (`package.json`, `uno.config.mjs`, `template.html`).
  - Bei jeder Änderung an HTML-Klassen in `template.html` oder `EmbeddedWebDropServer.cs` muss das Stylesheet vor `dotnet build` neu kompiliert werden:
    ```powershell
    $env:npm_config_cache = "D:\packages\npm-cache"
    Push-Location src/burroQueue.WebDrop
    npm run build:css
    Pop-Location
    ```
  - Das kompilierte `src/burroQueue.Infrastructure/Web/Assets/webdrop.uno.min.css` wird als `<EmbeddedResource>` eingebettet, über `WebDropStylesheetProvider` als gecachtes UTF-8 `ReadOnlyMemory<byte>` bereitgestellt und unter `GET /assets/webdrop.css` ausgeliefert.

## 3. AGPL-3.0 Compliance
- Da `burroQueue` unter der **GNU AGPL-3.0** steht, muss das WebDrop-Interface im Footer zwingend den sichtbaren Hinweis auf `GNU AGPL-3.0` und den Link auf `https://github.com/T-Boyke/burroQueue` enthalten.
