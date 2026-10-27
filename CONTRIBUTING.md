# Beitragen zu burroQueue

Vielen Dank für dein Interesse, zu `burroQueue` beizutragen!

Dieses Projekt legt höchsten Wert auf handwerkliche Software-Qualität, nachvollziehbare Architektur und Wartbarkeit. Bitte lies diese Richtlinien sorgfältig durch, bevor du einen Pull Request einreichst.

---

## 1. Die 5 Eisernen Architektur-Gesetze

1. **DDD & Reinheit der Domain:** `burroQueue.Domain` kapselt alle Geschäftsregeln (`Playlist`, `Track`, `SkipVotePool`, Events) und darf **0** externe NuGet-Pakete und **0** Projekt-Referenzen besitzen.
2. **Test-Driven Development (TDD):** Keine Produktionscode-Änderung ohne vorherigen fehlschlagenden xUnit-Test in `burroQueue.Tests`.
3. **Single Component Per File (SFC / 1CPF):** Exakt 1 Typ pro `.cs`- oder `.xaml`-Datei. Der Dateiname muss exakt dem Typnamen entsprechen.
4. **Performance First:** Standby-RAM < 30 MB. Kein WebView2, kein Electron, keine unnötigen Heap-Allokationen auf Hot-Paths.
5. **Regelmäßige Atomic Commits:** Kleine, logische Commits nach **Conventional Commits 1.0.0** (`feat:`, `fix:`, `test:`, `refactor:`, `docs:`).

---

## 2. Lokaler Workflow

### Voraussetzungen

- Windows 11
- .NET 10 SDK
- PowerShell 7 (`pwsh`)

### Vorbereitung & Verifikation

Führe vor jedem Commit das Architektur-Gate aus:

```powershell
./scripts/verify-architecture.ps1
```

Führe die Tests und Format-Prüfung aus:

```powershell
dotnet format burroQueue.sln --verify-no-changes
dotnet build burroQueue.sln -warnaserror
dotnet test burroQueue.sln
```

---

## 3. Dokumentation & Entscheidungen

- Aktualisiere `memory.md`, wenn du neue Features hinzufügst oder den Projektstatus veränderst.
- Führe neue architektonische Weichenstellungen in `decisions.md` als Architecture Decision Record (ADR) ein.
