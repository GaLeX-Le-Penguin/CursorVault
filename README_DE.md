# CursorVault

<div align="center">

[🇫🇷 Français](README.md) · [🇬🇧 English](README_EN.md) · [🇪🇸 Español](README_ES.md) · 🇩🇪 **Deutsch** · [🇮🇹 Italiano](README_IT.md)

### Moderner Cursor-Manager für Windows

Verwalte, importiere, aktiviere, sichere und organisiere deine Cursor-Pakete in einer einzigen Anwendung.

[![Latest release](https://img.shields.io/github/v/release/GaLeX-Le-Penguin/CursorVault?display_name=tag&sort=semver&label=version)](https://github.com/GaLeX-Le-Penguin/CursorVault/releases/latest)
[![Downloads](https://img.shields.io/github/downloads/GaLeX-Le-Penguin/CursorVault/total?label=downloads)](https://github.com/GaLeX-Le-Penguin/CursorVault/releases)
![Windows](https://img.shields.io/badge/Windows-10%20%7C%2011-0078D4?logo=windows11&logoColor=white)
![Architecture](https://img.shields.io/badge/architecture-x64-informational)
![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white)

### [⬇️ CursorVault herunterladen](https://github.com/GaLeX-Le-Penguin/CursorVault/releases/latest)

[Neueste Version](https://github.com/GaLeX-Le-Penguin/CursorVault/releases/latest) ·
[Releases](https://github.com/GaLeX-Le-Penguin/CursorVault/releases) ·
[Problem melden](https://github.com/GaLeX-Le-Penguin/CursorVault/issues)

</div>

---

## Über CursorVault

**CursorVault** ist eine von **GLX** entwickelte Windows-Anwendung zur einfachen und zentralen Verwaltung von Windows-Cursorn.

Du kannst `.cur`- und `.ani`-Dateien importieren, vollständige Cursor-Pakete organisieren, schnell ein neues Cursor-Design anwenden und bereits in Windows vorhandene Schemas verwalten.

Das Ziel ist einfach: eine moderne und praktische Alternative zur manuellen Cursor-Konfiguration über die Windows-Systemsteuerung.

---

## Vorschau

### Startseite

![CursorVault Startseite](docs/screenshots/home.png)

### Bibliothek

![CursorVault Bibliothek](docs/screenshots/library.png)

### Windows-Cursor

![Windows-Cursor](docs/screenshots/windows.png)

### Diagnose

![CursorVault Diagnose](docs/screenshots/Diagnostic.png)

### Einstellungen

![CursorVault Einstellungen](docs/screenshots/Settings.png)

---

## Funktionen

### 📚 Cursor-Bibliothek

- Lokale Paketbibliothek
- Favoriten
- Suche nach Name, Ersteller und Beschreibung
- Filter für vollständige, unvollständige, animierte, statische und favorisierte Pakete
- Sortierung nach Favoriten, Name, Ersteller, Vollständigkeit oder Hinzufügedatum
- Erkennung von Light-/Dark-Varianten
- Zufällige Paketauswahl
- Anzeige des ursprünglichen Erstellers

### 📥 Import

- Ordner importieren
- ZIP-Archive importieren
- `.cur`-Dateien importieren
- `.ani`-Dateien importieren
- Drag & Drop direkt in CursorVault
- Automatische lokale Installation
- Duplikaterkennung
- Paket als ZIP exportieren

### 🛠️ Erstellung und Analyse

- Integrierter Paket-Editor
- Pakete umbenennen
- Erhalt der Nennung des ursprünglichen Erstellers
- Analyse verfügbarer Windows-Cursorrollen
- Erkennung fehlender Dateien
- Erkennung ungültiger Dateien
- Duplikaterkennung
- CUR- und ANI-Validierung
- Reparatur fehlerhafter Referenzen
- Erstellung einer `install.inf`

### 🖱️ Windows-Integration

- Automatische Anwendung vollständiger Pakete
- Verwaltung der Windows-Cursorrollen
- Anzeige bereits installierter Windows-Schemas
- Erkennung des aktiven Schemas
- Direkte Anwendung von Windows-Schemas
- Zugriff auf die Windows-Zeigereinstellungen
- Zugriff auf den System-Cursorordner
- Optionale Synchronisierung mit dem hellen / dunklen Windows-Design

### ⭐ Automatisierung

- Automatische Paketrotation
- Rotation beim Start
- Stündliche Rotation
- Tägliche Rotation
- Rotation nur mit Favoriten
- Mit Windows starten
- In den Infobereich minimieren

### 💾 Sicherung

- Sicherung des aktuellen Windows-Cursorschemas
- Vollständige `.cvb`-Sicherung
- Wiederherstellung der Einstellungen
- Wiederherstellung der Pakete
- Wiederherstellung der Favoriten
- Portabler Modus
- Speicherverwaltung
- Cache-Bereinigung

### 🎨 Anpassung

- Dunkles Design
- Helles Design
- Anpassbare Farbe
- Auswahl installierter Windows-Schriftarten
- Kompakte, normale oder große Oberfläche
- Einstellungen zurücksetzen

### 🌍 Sprachen

CursorVault kann automatisch die Windows-Anzeigesprache verwenden.

Verfügbare Sprachen:

- Français
- English
- Español
- Deutsch
- Italiano

Die Sprache kann auch manuell in den Einstellungen ausgewählt werden.

### 🔍 Diagnose

- Integrierte Diagnose-Seite
- Systeminformationen
- Informationen zur CursorVault-Konfiguration
- Diagnose in die Zwischenablage kopieren
- Speicherinformationen
- Cache-Bereinigung

---

## Installation

1. Öffne die [neueste CursorVault-Version](https://github.com/GaLeX-Le-Penguin/CursorVault/releases/latest)
2. Lade **`CursorVault.zip`** herunter
3. Entpacke das Archiv vollständig
4. Starte **`CursorVault.exe`**

```text
CursorVault/
├── CursorVault.exe
└── CursorVault_Data/
```

Für die offizielle Version ist keine separate .NET-Installation erforderlich.

---

## Systemanforderungen

- Windows 10 oder Windows 11
- x64-Architektur
- Keine separate .NET-Installation erforderlich

---

## Updates

CursorVault kann über **GitHub Releases** automatisch prüfen, ob eine neue Version verfügbar ist.

### [⬇️ Neueste Version herunterladen](https://github.com/GaLeX-Le-Penguin/CursorVault/releases/latest)

---

## Benutzerdaten

CursorVault speichert die für den Betrieb erforderlichen Daten lokal, darunter importierte Pakete, Favoriten, Einstellungen, Sicherungen und Cache-Dateien.

Ein **portabler Modus** ermöglicht außerdem, die Daten zusammen mit der Anwendung zu speichern.

---

## Unterstützte Formate

- `.cur`
- `.ani`

Pakete werden vor der Anwendung analysiert, um fehlende oder ungültige Dateien zu erkennen.

---

## Credits

CursorVault kann Cursor-Pakete verschiedener Autoren enthalten oder unterstützen.

Der ursprüngliche Ersteller eines Pakets wird in der Anwendung angezeigt, sofern er bekannt ist.

Pakete von Drittanbietern bleiben Eigentum ihrer jeweiligen Autoren.

CursorVault und seine visuelle Identität werden von **GLX** entwickelt.

---

## Problem melden

Du hast einen Fehler gefunden oder möchtest eine Verbesserung vorschlagen?

Nutze die [GitHub Issues](https://github.com/GaLeX-Le-Penguin/CursorVault/issues).

Gib nach Möglichkeit die CursorVault-Version, die Windows-Version, Schritte zur Reproduktion, die Fehlermeldung, einen Screenshot und die CursorVault-Diagnose an.

---

## Kompatibilität

| System | Unterstützung |
|---|---|
| Windows 11 x64 | ✅ |
| Windows 10 x64 | ✅ |
| Windows ARM | Nicht getestet |
| Linux | ❌ |
| macOS | ❌ |

---

## Copyright

**CursorVault**  
Copyright © 2026 GLX

CursorVault und seine visuelle Identität werden von **GLX** entwickelt.

Erstellungen und Cursor-Pakete von Drittanbietern bleiben Eigentum ihrer jeweiligen Autoren.

---

<div align="center">

### CursorVault

Entwickelt von **GLX** für Windows.

[⬇️ Herunterladen](https://github.com/GaLeX-Le-Penguin/CursorVault/releases/latest) ·
[Releases](https://github.com/GaLeX-Le-Penguin/CursorVault/releases) ·
[Issues](https://github.com/GaLeX-Le-Penguin/CursorVault/issues)

</div>
