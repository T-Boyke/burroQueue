---
name: youtube-network-sync
description: >-
  Guides the implementation of YouTube URL parsing/validation and the lightweight local network
  queue listener in burroQueue.Infrastructure. Use this skill when working on YouTube link extraction,
  local HTTP/LAN endpoints for coworkers, or AGPL-3.0 network compliance.
---

# YouTube & Local Network Sync Skill (`burroQueue.Infrastructure`)

Dieser Skill definiert die Vorgaben für externe Integrationen in `src/burroQueue.Infrastructure`.

## 1. YouTube-Link-Verarbeitung
- Unterstützte URL-Formate müssen zuverlässig und ohne Regex-Backtracking-Risiko (oder via `Uri`-Parsing + `[GeneratedRegex]`) erkannt werden:
  - `https://www.youtube.com/watch?v=<11-char-id>`
  - `https://youtu.be/<11-char-id>`
  - `https://music.youtube.com/watch?v=<11-char-id>`
  - `https://www.youtube.com/shorts/<11-char-id>`
- Jede Komponente (Interface `IYouTubeUrlParser`, Implementierung `YouTubeUrlParser`) liegt gemäß **1CPF** in einer eigenen Datei.
- Alle Parser-Fälle (gültige URLs, zusätzliche Query-Parameter wie `&t=42s`, ungültige Domains, leere Strings) müssen per `[Theory]` / `[InlineData]` in `burroQueue.Tests` abgedeckt sein.

## 2. Lightweight Local Network Sync (LAN-Endpoint)
- Mitarbeiter im Büro sollen ohne lokale Installation Links über das lokale Netzwerk in die `burroQueue` werfen können.
- **Ressourcen-Disziplin (< 30 MB RAM):**
  - Nutze `System.Net.HttpListener` oder einen minimalen Socket/HTTP-Handler ohne schwere Web-Frameworks.
  - Biete einen schlanken `POST /api/queue`-Endpunkt sowie eine winzige, statisch im Speicher gehaltene HTML-Drop-Seite (`GET /`) im Fluent-Design-Stil an.
  - Unterstütze sauberes Starten und Stoppen über `CancellationToken` und `IAsyncDisposable`.

## 3. AGPL-3.0 Compliance
- Da `burroQueue` unter der **GNU AGPL-3.0** steht, muss das lokale Web-Interface im Footer einen dezenten Link auf das Quellcode-Repository (`https://github.com/T-Boyke/burroQueue`) und die AGPL-3.0-Lizenz enthalten.

