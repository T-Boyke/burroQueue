# Anti-AI-Slop & Craftsmanship Richtlinien

## 1. Verbot von Design- und UI-Slop
- Verwende in XAML-Dateien niemals willkürliche violette/blaue Farbverläufe oder generische Vorlagen-Elemente.
- Setze auf echtes Windows 11 Fluent Design mit `Mica`-Backdrop, systemnaher Farbpalette und klarer typografischer Hierarchie (`Segoe UI Variable`).
- Vermeide Emoji-Überladungen und seelenlose Marketing-Floskeln.

## 2. Verbot von Code-Slop
- Keine generierten, redundanten Methoden-Kommentare, die nur den Methodennamen wiederholen.
- Keine `try { ... } catch (Exception) { }` Schlucker ohne bewusste Strategie.
- Striktes Einhalten von **1CPF / SFC (Single Component Per File)** und **DDD-Reinheit**.
- Keine unbegründeten Allokationen auf Hot-Paths (< 30 MB Standby RAM).
