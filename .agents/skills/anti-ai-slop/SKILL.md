---
name: anti-ai-slop
description: >-
  Detect and eliminate AI slop, generic template aesthetics, and mindless code patterns in burroQueue.
  Use this skill whenever designing or modifying Windows 11 Fluent UI components, XAML styling,
  C# architecture, or reviewing code and copy to ensure authentic craft, crisp hierarchy, and zero slop.
---

# Anti-AI-Slop & Craftsmanship Skill (`burroQueue`)

Dieser Skill erzwingt handwerkliche Exzellenz, visuelle Differenzierung und kompromisslose Code-Qualität in `burroQueue`.

---

## 1. Was ist "AI-Slop"?

AI-Slop ist die **statistische Konvergenz zum Durchschnitt**:
- In UIs: Seelenlose lila/blaue Verläufe, übertriebenes Glasmorphismus-Geflacker, Inter-Monokultur, inflationäre Emojis (🚀⚡🎯), generische SaaS-Karten ohne Informationsdichte.
- Im Code: Aufgeblähte Boilerplate-Kommentare ("This method calculates..."), sinnlose Wrapper-Klassen ohne Abstraktionsgewinn, Catch-All `catch(Exception)` ohne sinnvolle Behandlung, nichtssagende Variablennamen (`data`, `item`, `res`).
- Im Text: "Transform your workflow effortlessly", "seamlessly revolutionize", Bindestrich-Pausen („em-dash“).

---

## 2. Visuelle Leitlinien für `burroQueue` (Windows 11 Fluent UI)

### ❌ Zu vermeidender UI-Slop:
- Keine willkürlichen CSS/WPF-Farbverläufe (z. B. Lila-auf-Blau oder Neon-Glow).
- Kein Fake-Glasmorphismus: Nutze echtes Windows 11 `Mica` (`WindowBackdropType="Mica"`).
- Kein Emoji-Spamming im Fließtext.
- Keine riesigen leeren Flächen ohne Informationsgehalt.

### ✅ Erwünschte Design-Prinzipien:
1. **Windows 11 Authentizität:** Fluent Design mit System-Schriftarten (`Segoe UI Variable Text/Display`), abgerundeten Ecken (`WindowCornerPreference="Round"`) und Mica-Material.
2. **Echte Haptik & States:** Jeder interaktive Button hat klare `MouseOver`-, `Pressed`- und `Disabled`-Zustände.
3. **Präzise Informationsdichte:** Album-Cover scharf skaliert, Video-Titel mit `TextTrimming="CharacterEllipsis"`, klare Kontraste für gute Lesbarkeit im hellen und dunklen Büro.
4. **Branding:** Dezente, stolze Verwendung des offiziellen `burroQueue`-Logos in Systray, Titelleiste und App.

---

## 3. Code-Slop Checkliste (C# 14 / .NET 10)

Vor jedem Commit prüfen:
- [ ] **SFC / 1CPF Gesetz:** Exakt 1 Typ pro `.cs`-Datei. Keine verschachtelten Klassen.
- [ ] **DDD Reinheit:** `burroQueue.Domain` hat 0 externe Paket-Abhängigkeiten und kennt weder WPF noch System.Net.
- [ ] **Keine toten Kommentare:** Schreibe Code so selbsterklärend, dass triviale Kommentare entfallen. Erkläre nur das *Warum*, niemals das *Was*.
- [ ] **Sparsamer Heap-Verbrauch:** Kleine Value Objects sind `readonly record struct`, Collections im Domain-Modell sind `IReadOnlyList<T>` statt mutabler Listen.
- [ ] **Nullability-Disziplin:** Kein unbedachtes `!` (Null-Forgiving-Operator).

