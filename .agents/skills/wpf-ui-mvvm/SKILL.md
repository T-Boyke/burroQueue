---
name: wpf-ui-mvvm
description: >-
  Guides the creation and modification of Windows 11 Fluent UI components, Mica/Acrylic windows,
  Taskbar/Systray integration, Drag-and-Drop zones, and MVVM ViewModels using Wpf.Ui and
  CommunityToolkit.Mvvm in burroQueue.Presentation.
---

# WPF.UI & MVVM Component Skill (`burroQueue.Presentation`)

Nutze diesen Skill für alle Arbeiten im Projekt `src/burroQueue.Presentation`.

## Design- & Performance-Leitlinien
1. **Windows 11 Fluent Design:**
   - Haupt- und Flyout-Fenster erben von `Wpf.Ui.Controls.FluentWindow`.
   - Nutze `WindowBackdropType="Mica"` und `ExtendsContentIntoTitleBar="True"`.
   - Unterstütze automatischen Light/Dark-Mode-Wechsel über `SystemThemeWatcher.Watch(this)`.
2. **Taskbar Drop-Zone & Systray:**
   - Die Drop-Zone nimmt Links per Drag-and-Drop (`DataFormats.Text`, `DataFormats.UnicodeText`, Browser-URLs) und per Zwischenablage (`Ctrl+V`) entgegen.
   - `PreviewDragOver` prüft synchron und extrem schnell, ob Text/URI vorliegt; das eigentliche Enqueueing läuft über das `RelayCommand` im ViewModel.
3. **Strict MVVM mit `CommunityToolkit.Mvvm`:**
   - **Null Business-Logik im Code-Behind (`.xaml.cs`)!** Code-Behind ist ausschließlich für View-Initialisierung, ThemeWatcher und das Weiterleiten von UI-Drag-Events an ViewModel-Commands zuständig.
   - ViewModels sind `public sealed partial class` und erben von `ObservableObject`.
   - Nutze `[ObservableProperty]` und `[RelayCommand]` Source Generators statt manueller `INotifyPropertyChanged`-Implementierung.
4. **SFC / 1CPF & RAM-Budget (< 30 MB):**
   - 1 View (`.xaml` + `.xaml.cs`) pro Datei-Paar, 1 ViewModel pro `.cs`-Datei.
   - Keine schweren eingebetteten Browser (`WebView2`).
   - Bilder/Thumbnails nur bei Bedarf und mit `DecodePixelWidth` laden, um den RAM-Verbrauch minimal zu halten.

