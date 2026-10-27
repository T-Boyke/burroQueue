# Schichtentrennung & SFC / 1CPF Durchsetzung

## Dependency-Richtung
Die Abhängigkeiten zwischen den Projekten unter `src/` dürfen ausschließlich wie folgt verlaufen:

- `burroQueue.Domain` -> **keine Abhängigkeiten**
- `burroQueue.Infrastructure` -> `burroQueue.Domain`
- `burroQueue.Presentation` -> `burroQueue.Domain`, `burroQueue.Infrastructure`
- `burroQueue.Tests` -> `burroQueue.Domain`, `burroQueue.Infrastructure`, `burroQueue.Presentation`

## Regeln für Datei-Organisation (1CPF)
1. **Ein Interface = Eine Datei:** Lege ein Interface `IYouTubeUrlParser` niemals in dieselbe Datei wie `YouTubeUrlParser`.
2. **Ein Enum / Record = Eine Datei:** Auch kleine Hilfstypen wie `TrackId` oder `PlaybackState` erhalten jeweils ihre eigene `.cs`-Datei.
3. **Exceptions:** Jede spezifische Domain-Exception liegt als eigene Datei unter `src/burroQueue.Domain/Exceptions/`.
4. **Tests:** Zu jeder produktiven Komponente `<Component>.cs` gehört eine dedizierte Testklasse `<Component>Tests.cs` im korrespondierenden Unterordner von `src/burroQueue.Tests/`.

