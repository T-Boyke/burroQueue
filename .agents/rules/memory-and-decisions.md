# Regel: Kontinuierliche Pflege von memory.md und decisions.md

Diese Regel ist **verbindlich** für alle Arbeiten im Repository `burroQueue`.

## 1. `memory.md` (Projekt-Gedächtnis & Status)

- Befindet sich im Repository-Root (`memory.md`).
- Enthält den aktuellen Gesamtzustand des Projekts, aktive Features, bekannte Randfälle, Testabdeckung und den Fortschritt zu Version 1.0.
- Muss bei jedem größeren Meilenstein und bei signifikanten Code-Änderungen aktualisiert werden.
- Dient als zentrale Wissensbasis für Entwickler und KI-Agenten, um Context-Verluste bei langen Sessions zu verhindern.

## 2. `decisions.md` (Architecture Decision Records - ADR)

- Befindet sich im Repository-Root (`decisions.md`).
- Hält alle wesentlichen technischen und architektonischen Entscheidungen im ADR-Format fest:
  - **Titel:** ADR-XXX: [Kurzbeschreibung]
  - **Status:** Angenommen / Abgelehnt / Ersetzt
  - **Kontext:** Welches Problem lag vor? Welche Rahmenbedingungen galten (z. B. < 30 MB RAM, DDD-Reinheit, Win11 Fluent)?
  - **Entscheidung:** Was wurde implementiert und warum?
  - **Konsequenzen:** Welche Vor- und Nachteile bzw. Verpflichtungen ergeben sich daraus?
- Jede neue architektonische Weichenstellung (Netzwerk-Protokoll, Audio-Player, Test-Harness, CI-Pipeline) muss mit einem ADR dokumentiert werden.
